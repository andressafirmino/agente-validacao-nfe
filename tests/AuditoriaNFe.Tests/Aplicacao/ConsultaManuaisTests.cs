using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Aplicacao;

/// <summary>Etapa 5, consulta a manuais oficiais (FR-047a a FR-047d, FR-022, FR-024).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class ConsultaManuaisTests
{
    private readonly AmbienteIntegracao _ambiente;

    public ConsultaManuaisTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MCP_desabilitado_etapa_nao_aplicavel_com_limitacao_e_revisao_humana_TS046()
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(), [ArquivoEnvio.Fixture(NFeValida)], cancelamento: Cancelamento));

        var etapa = Etapa(envelope, 5);
        Assert.Equal("NaoAplicavel", etapa.Estado);
        Assert.Contains("integração não habilitada", etapa.Justificativa, StringComparison.Ordinal);
        Assert.Contains("consulta a manuais oficiais não habilitada", envelope.Parecer!.Limitacoes);
        Assert.True(envelope.Parecer.RevisaoHumana.Necessaria);
        Assert.Equal("SemInconsistencias", envelope.Parecer.ResultadoGeral);
        Assert.False(envelope.Parecer.IntegracaoMcp.Habilitada);
    }

    [Fact]
    public async Task MCP_habilitado_com_servidor_inacessivel_resulta_em_ERR013_e_inconclusiva_TS047()
    {
        var fabrica = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?>
        {
            ["Mcp:Habilitado"] = "true",
            ["Mcp:Endpoint"] = "http://127.0.0.1:9/mcp",
            ["Mcp:Timeout"] = "00:00:02",
        });
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            fabrica.CreateClient(), [ArquivoEnvio.Fixture(NFeValida)], cancelamento: Cancelamento));

        FalhaDeDependencia(envelope, CodigoErro.BaseConhecimentoIndisponivel);
        Assert.Equal("Inconclusiva", envelope.Parecer!.ResultadoGeral);
        Assert.Equal("Falhou", Etapa(envelope, 5).Estado);
        Assert.True(envelope.Parecer.IntegracaoMcp.Habilitada);
    }

    [Fact]
    public async Task Base_de_conhecimento_que_falha_resulta_em_ERR013_e_inconclusiva_TS026()
    {
        var fabrica = FabricaComConsulta(new ConsultaManuaisFalsa(erro: new ConsultaManuaisIndisponivelException("Base indisponível.")));
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            fabrica.CreateClient(), [ArquivoEnvio.Fixture(NFeValida)], cancelamento: Cancelamento));

        FalhaDeDependencia(envelope, CodigoErro.BaseConhecimentoIndisponivel);
        Assert.Equal("Inconclusiva", envelope.Parecer!.ResultadoGeral);
    }

    [Fact]
    public async Task Base_sem_trechos_registra_ausencia_de_evidencias_e_revisao_humana_TS038()
    {
        var consulta = new ConsultaManuaisFalsa();
        var fabrica = FabricaComConsulta(consulta);
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            fabrica.CreateClient(), [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)], cancelamento: Cancelamento));

        Assert.NotEmpty(consulta.Consultas);
        Assert.Equal("Executada", Etapa(envelope, 5).Estado);
        Assert.Contains(envelope.Parecer!.Achados, a => a.Categoria == "AusenciaEvidencias");
        Assert.True(envelope.Parecer.RevisaoHumana.Necessaria);
        Assert.Equal("ComInconsistencias", envelope.Parecer.ResultadoGeral);
        Assert.True(envelope.Parecer.IntegracaoMcp.Habilitada);
        Assert.Null(envelope.Erro);
    }

    [Fact]
    public async Task Consulta_leva_data_de_referencia_e_regras_sem_o_XML_nem_dados_pessoais()
    {
        var consulta = new ConsultaManuaisFalsa();
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        await ClienteExecucoes.CriarAsync(
            FabricaComConsulta(consulta).CreateClient(), [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)], cancelamento: Cancelamento);

        var enviada = Assert.Single(consulta.Consultas);
        Assert.Equal(("NF-e", "55", "4.00"), (enviada.TipoDocumento, enviada.Modelo, enviada.VersaoLayout));
        Assert.Equal(new DateOnly(2026, 11, 2), enviada.DataReferencia);
        Assert.Contains("W04-10", enviada.RegrasRelacionadas);
        Assert.DoesNotContain("<", enviada.Consulta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trechos_recuperados_ficam_registrados_na_execucao()
    {
        var consulta = new ConsultaManuaisFalsa([ConsultaManuaisFalsa.Trecho()]);
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            FabricaComConsulta(consulta).CreateClient(), [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)], cancelamento: Cancelamento));

        Assert.Equal("Executada", Etapa(envelope, 5).Estado);
        Assert.DoesNotContain(envelope.Parecer!.Achados, a => a.Categoria == "AusenciaEvidencias");
        Assert.Contains("manual-sintetico-1.0-001", string.Join("\n", _ambiente.ChatClient.Chamadas.SelectMany(c => c.Mensagens).Select(m => m.Text)), StringComparison.Ordinal);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> FabricaComConsulta(ConsultaManuaisFalsa consulta) =>
        _ambiente.CriarFabrica(
            configuracoes: new Dictionary<string, string?> { ["Mcp:Habilitado"] = "true", ["Mcp:Endpoint"] = "http://127.0.0.1:9/mcp" },
            servicos: s =>
            {
                s.RemoveAll<IConsultaManuais>();
                s.AddSingleton<IConsultaManuais>(consulta);
            });
}
