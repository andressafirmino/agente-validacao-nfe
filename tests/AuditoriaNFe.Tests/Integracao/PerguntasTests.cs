using AuditoriaNFe.Application.Ferramentas;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>TS-014 e TS-015 ponta a ponta (T111).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class IntegracaoPerguntasTests
{
    private const string CampoVnf = "total/ICMSTot/vNF";
    private readonly AmbienteIntegracao _ambiente;

    public IntegracaoPerguntasTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pergunta_com_sessao_analisa_base_TS014_TS015()
    {
        var lote = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            [ArquivoEnvio.Fixture(NFeValida)],
            cancelamento: Cancelamento));

        _ambiente.ChatClient.Responder(RespostasIa.ComIndisponibilidade(
            "Não há o valor pedido na base analisada desta sessão."));

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            pergunta: "Qual o ICMS monofásico total?",
            sessaoId: lote.SessaoId,
            cancelamento: Cancelamento));

        Assert.NotNull(envelope.Resposta);
        Assert.True(envelope.Resposta!.InformacaoIndisponivel);
    }
}
