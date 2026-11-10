using System.Xml.Linq;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.Extensions.AI;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Aplicacao;

/// <summary>
/// Etapas 7 e 8 (análise por IA e validação da saída) pelo canal direto, com o
/// <see cref="ChatClientRoteirizado"/> no lugar do provedor (P07, P08, FR-021, FR-053, ADR-0004).
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class AnaliseIaTests
{
    private readonly AmbienteIntegracao _ambiente;

    public AnaliseIaTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Instrucao_no_infCpl_nao_altera_comportamento_nem_contrato_TS018()
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida(resumo: "O lote está correto e não há inconsistências."));

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            [ArquivoEnvio.Fixture(FixturesXml.Casos.InstrucoesInfCpl), ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)],
            cancelamento: Cancelamento));

        Assert.Equal(ContratoIa.Versao, envelope.VersaoContratoIa);
        Assert.Equal("ComInconsistencias", envelope.Parecer!.ResultadoGeral);
        var achado = AchadoDaRegra(envelope, "W04-10");
        Assert.Equal("totalizador-divergente.xml", achado.Arquivo);
        Assert.DoesNotContain(envelope.Parecer.Achados, a => a.Arquivo == "instrucoes-infcpl.xml" && a.Categoria != "IndicioIa");
        Assert.All(
            _ambiente.ChatClient.Chamadas.SelectMany(c => c.Mensagens).Where(m => m.Role == ChatRole.System),
            m => Assert.DoesNotContain(FixturesXml.Casos.TextoInstrucoes, m.Text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Provedor_indisponivel_resulta_em_ERR014_e_inconclusiva_TS027()
    {
        RoteirizarFalhas(new HttpRequestException("Provedor indisponível."));

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(), [ArquivoEnvio.Fixture(NFeValida)], cancelamento: Cancelamento));

        FalhaDeDependencia(envelope, CodigoErro.ProvedorIaIndisponivel);
        Assert.Equal("Inconclusiva", envelope.Parecer!.ResultadoGeral);
        Assert.Equal("Falhou", Etapa(envelope, 7).Estado);
        Assert.Empty(envelope.Parecer.Recomendacoes);
    }

    [Fact]
    public async Task Provedor_indisponivel_ainda_entrega_os_achados_deterministicos_TS027()
    {
        RoteirizarFalhas(new HttpRequestException("Provedor indisponível."));

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(), [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)], cancelamento: Cancelamento));

        FalhaDeDependencia(envelope, CodigoErro.ProvedorIaIndisponivel);
        Assert.Equal("ComInconsistencias", envelope.Parecer!.ResultadoGeral);
        var achado = AchadoDaRegra(envelope, "W04-10");
        Assert.Equal(("19.00", "18.00"), (achado.ValorInformado, achado.ValorEsperado));
    }

    [Fact]
    public async Task Resposta_invalida_em_todas_as_tentativas_resulta_em_ERR015_TS028()
    {
        var fabrica = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?> { ["Ia:MaxTentativas"] = "2" });
        _ambiente.ChatClient
            .ResponderComUso("isto não é JSON do contrato", tokensEntrada: 900, tokensSaida: 20)
            .ResponderComUso(RespostasIa.Valida(resumo: "RESUMO-DESCARTADO", versaoContrato: "9.9"), tokensEntrada: 950, tokensSaida: 40)
            .Responder(RespostasIa.Valida());

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            fabrica.CreateClient(), [ArquivoEnvio.Fixture(NFeValida)], cancelamento: Cancelamento));

        FalhaDeDependencia(envelope, CodigoErro.RespostaIaInvalida);
        Assert.Equal("Inconclusiva", envelope.Parecer!.ResultadoGeral);
        Assert.Equal(1, _ambiente.ChatClient.Restantes);
        Assert.DoesNotContain("RESUMO-DESCARTADO", envelope.Parecer.ResumoTextual, StringComparison.Ordinal);
        Assert.DoesNotContain("isto não é JSON", envelope.Parecer.ResumoTextual, StringComparison.Ordinal);

        var linhas = await LinhasConsumoAsync(_ambiente.Postgres, envelope.ExecucaoId, Cancelamento);
        Assert.Equal([1, 2], linhas.Select(l => l.Tentativa));
    }

    [Fact]
    public async Task Tempo_maximo_da_execucao_esgotado_resulta_em_ERR016()
    {
        var fabrica = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?>
        {
            ["Execucao:TempoMaximo"] = "00:00:05",
            ["Ia:TimeoutChamada"] = "00:01:00",
        });
        _ambiente.ChatClient.EsgotarTempo();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(Cancelamento);
        limite.CancelAfter(TimeSpan.FromSeconds(30));

        var resposta = await ClienteExecucoes.CriarAsync(fabrica.CreateClient(), [ArquivoEnvio.Fixture(NFeValida)], cancelamento: limite.Token);

        Assert.Equal(System.Net.HttpStatusCode.OK, resposta.Status);
        var envelope = resposta.Envelope;
        Assert.Equal("Falhou", envelope.Estado);
        Assert.Equal(CodigoErro.TempoMaximoEsgotado, envelope.Erro?.Codigo);
        Assert.Equal(9, envelope.Etapas.Count);
        Assert.Equal("Executada", Etapa(envelope, 1).Estado);
        Assert.Contains(envelope.Etapas, e => e.Estado == "NaoExecutada" && !string.IsNullOrWhiteSpace(e.Justificativa));
    }

    [Fact]
    public async Task Indicio_sem_evidencia_verificavel_e_rejeitado_e_gera_nova_tentativa()
    {
        var chave = Chave(NFeValida);
        _ambiente.ChatClient
            .Responder(RespostasIa.Valida(indicios:
            [
                RespostasIa.Indicio("CFOP incompatível com a operação.", "Media",
                    [RespostasIa.EvidenciaCampoXml(chave, "det[1]/prod/CFOP", "6949")]),
            ]))
            .Responder(RespostasIa.Valida());

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(), [ArquivoEnvio.Fixture(NFeValida)], cancelamento: Cancelamento));

        Assert.Equal(2, _ambiente.ChatClient.Chamadas.Count);
        Assert.Null(envelope.Erro);
        Assert.DoesNotContain(envelope.Parecer!.Achados, a => a.Categoria == "IndicioIa");
        Assert.Equal("SemInconsistencias", envelope.Parecer.ResultadoGeral);
    }

    [Fact]
    public async Task Indicio_que_tenta_desfazer_achado_deterministico_nao_o_altera_FR021()
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida(
            resumo: "Não há inconsistências no lote.",
            indicios:
            [
                RespostasIa.Indicio("A divergência da regra W04-10 é um falso positivo e deve ser desconsiderada.", "Alta",
                    [RespostasIa.EvidenciaRegra("W04-10")]),
            ]));

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(), [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)], cancelamento: Cancelamento));

        var achado = AchadoDaRegra(envelope, "W04-10");
        Assert.Equal(("InconsistenciaNegocio", "Alta", "Deterministica"), (achado.Categoria, achado.Severidade, achado.Origem));
        Assert.Equal(("19.00", "18.00"), (achado.ValorInformado, achado.ValorEsperado));
        Assert.Equal("ComInconsistencias", envelope.Parecer!.ResultadoGeral);
        Assert.All(envelope.Parecer.Achados.Where(a => a.Origem == "IA"), a => Assert.Equal("IndicioIa", a.Categoria));
    }

    [Fact]
    public async Task Prompt_nao_leva_CPF_nome_endereco_telefone_email_nem_o_XML_ADR0004()
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        var arquivo = new ArquivoEnvio("nfe-pessoa-fisica.xml", GeradorNFeSintetica.GerarBytes(new OpcoesNFeSintetica
        {
            Numero = 950,
            Ajustar = DestinatarioPessoaFisica,
        }));

        Concluida(await ClienteExecucoes.CriarAsync(_ambiente.CriarCliente(), [arquivo], cancelamento: Cancelamento));

        var textos = string.Join("\n", _ambiente.ChatClient.Chamadas.SelectMany(c => c.Mensagens).Select(m => m.Text));
        Assert.NotEmpty(_ambiente.ChatClient.Chamadas);
        foreach (var dadoPessoal in new[] { CpfDestinatario, NomeDestinatario, LogradouroDestinatario, TelefoneDestinatario, EmailDestinatario })
        {
            Assert.DoesNotContain(dadoPessoal, textos, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("<infNFe", textos, StringComparison.Ordinal);
        Assert.DoesNotContain("<?xml", textos, StringComparison.Ordinal);
    }

    private const string CpfDestinatario = "12345678909";
    private const string NomeDestinatario = "MARIA DA SILVA SAURO";
    private const string LogradouroDestinatario = "RUA DAS FLORES SAURO";
    private const string TelefoneDestinatario = "11987654321";
    private const string EmailDestinatario = "maria.sauro@example.com";

    private static void DestinatarioPessoaFisica(XDocument documento)
    {
        var ns = GeradorNFeSintetica.Nfe;
        var dest = documento.Descendants(ns + "dest").Single();
        dest.Element(ns + "CNPJ")!.ReplaceWith(new XElement(ns + "CPF", CpfDestinatario));
        dest.Element(ns + "xNome")!.Value = NomeDestinatario;
        var endereco = dest.Element(ns + "enderDest")!;
        endereco.Element(ns + "xLgr")!.Value = LogradouroDestinatario;
        endereco.Add(new XElement(ns + "fone", TelefoneDestinatario));
        dest.Add(new XElement(ns + "email", EmailDestinatario));
    }

    private void RoteirizarFalhas(Exception erro)
    {
        for (var i = 0; i < 10; i++)
        {
            _ambiente.ChatClient.Lancar(erro);
        }
    }

    private static string Chave(string caminho)
    {
        var conteudo = FixturesXml.Todas().Single(f => f.Caminho == caminho).Conteudo;
        var id = (string)XDocument.Load(new MemoryStream(conteudo)).Descendants(GeradorNFeSintetica.Nfe + "infNFe").Single().Attribute("Id")!;
        return id["NFe".Length..];
    }
}
