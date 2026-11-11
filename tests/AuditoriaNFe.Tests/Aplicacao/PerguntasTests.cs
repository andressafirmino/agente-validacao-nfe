using System.Text.Json.Nodes;
using AuditoriaNFe.Application.ContextProviders;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Application.Ferramentas;
using AuditoriaNFe.Application.Servicos;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Aplicacao;

/// <summary>TS-014 a TS-017 e TS-037 com <see cref="ChatClientRoteirizado"/> (T110).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class PerguntasTests
{
    private const string CampoVnf = "total/ICMSTot/vNF";
    private readonly AmbienteIntegracao _ambiente;

    public PerguntasTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pergunta_sobre_valor_total_com_calculo_declarado_TS014()
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        var lote = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            [ArquivoEnvio.Fixture(NFeValida)],
            cancelamento: Cancelamento));

        _ambiente.ChatClient
            .ResponderInvocacao(PoliticaFerramentas.CalcularTotal, new { campo = CampoVnf })
            .ResponderAposFerramenta(RoteiroPerguntaComCalculo.RespostaAposCalcularTotal);

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            pergunta: "Qual o valor total da nota?",
            sessaoId: lote.SessaoId,
            cancelamento: Cancelamento));

        Assert.NotNull(envelope.Resposta);
        var citado = Assert.Single(envelope.Resposta!.ValoresCitados);
        Assert.NotNull(citado.CalculoId);
        Assert.Matches(@"^-?[0-9]+(\.[0-9]+)?$", citado.Valor);
    }

    [Fact]
    public async Task Pergunta_sobre_valor_inexistente_sem_estimativa_TS015()
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        var lote = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            [ArquivoEnvio.Fixture(NFeValida)],
            cancelamento: Cancelamento));

        _ambiente.ChatClient.Responder(RespostasIa.ComIndisponibilidade(
            "Não há informação financeira solicitada na base analisada."));

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            pergunta: "Qual o lucro líquido do emitente?",
            sessaoId: lote.SessaoId,
            cancelamento: Cancelamento));

        Assert.True(envelope.Resposta?.InformacaoIndisponivel);
        Assert.Empty(envelope.Resposta!.ValoresCitados);
        Assert.DoesNotContain("R$", envelope.Resposta.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pergunta_fora_do_escopo_TS017()
    {
        _ambiente.ChatClient.Responder(RespostasIa.ComForaDeEscopo(
            "O escopo deste agente é auditoria de NF-e modelo 55 versão 4.00."));

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            pergunta: "Qual a cotação do dólar hoje?",
            cancelamento: Cancelamento));

        Assert.True(envelope.Resposta?.ForaDeEscopo);
    }

    [Fact]
    public void Resposta_adversarial_com_valor_nao_rastreavel_falha_validacao_TS016()
    {
        var json = RespostasIa.Valida(resposta: new JsonObject
        {
            ["texto"] = "O total informado é R$ 1.000,00.",
            ["valoresCitados"] = new JsonArray(),
            ["documentosExcluidos"] = new JsonArray(),
        });
        var saida = System.Text.Json.JsonSerializer.Deserialize<ContratoIaV1>(json, ContratoIaV1.OpcoesJson)!;
        var contexto = ContextoMinimo();
        var problemas = ValidadorSaidaIa.Validar(saida, contexto);
        Assert.Contains(problemas, p => p.Contains("valoresCitados", StringComparison.Ordinal));
    }

    [Fact]
    public void Campo_nao_extraido_orienta_ressalva_TS037()
    {
        var json = RespostasIa.ComIndisponibilidade(
            "O campo não faz parte dos dados extraídos. Reenvie os XMLs para nova extração.");
        var saida = System.Text.Json.JsonSerializer.Deserialize<ContratoIaV1>(json, ContratoIaV1.OpcoesJson)!;
        var contexto = ContextoMinimo();
        Assert.Empty(ValidadorSaidaIa.Validar(saida, contexto));
    }

    private static ContextoAgenteIa ContextoMinimo() =>
        new(
            new ContextoExecucaoIa([], [], [], [], [], "Pergunta teste"),
            ContextoSessaoIa.Nova(semBaseAnalisada: true),
            TipoExecucao.Pergunta,
            [],
            null);

}
