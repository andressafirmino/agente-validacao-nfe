using AuditoriaNFe.Application.Fluxo;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>Api.RAG + cliente MCP real (etapas 5 e 6).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class McpApiRagIntegracaoTests : IAsyncDisposable
{
    private readonly AmbienteIntegracao _ambiente;
    private readonly ServidorApiRagMcp _rag = new();

    public McpApiRagIntegracaoTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _rag.DisposeAsync();

    [Fact]
    public async Task MCP_com_Api_RAG_recupera_trecho_sintetico_na_etapa_5()
    {
        await _rag.IniciarAsync(Cancelamento);
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var fabrica = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?>
        {
            ["Mcp:Habilitado"] = "true",
            ["Mcp:Endpoint"] = _rag.EndpointMcp,
            ["Mcp:FerramentasExternasHabilitado"] = "false",
        });

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            fabrica.CreateClient(),
            [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)],
            cancelamento: Cancelamento));

        Assert.Equal("Executada", Etapa(envelope, 5).Estado);
        Assert.Contains("nt-sintetico-w04-10-001", string.Join('\n', _ambiente.ChatClient.Chamadas.SelectMany(c => c.Mensagens).Select(m => m.Text)), StringComparison.Ordinal);
        Assert.Null(envelope.Erro);
    }

    [Fact]
    public async Task MCP_com_ferramentas_externas_habilitadas_executa_etapa_6()
    {
        await _rag.IniciarAsync(Cancelamento);
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var fabrica = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?>
        {
            ["Mcp:Habilitado"] = "true",
            ["Mcp:Endpoint"] = _rag.EndpointMcp,
            ["Mcp:FerramentasExternasHabilitado"] = "true",
        });

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            fabrica.CreateClient(),
            [ArquivoEnvio.Fixture(NFeValida)],
            cancelamento: Cancelamento));

        Assert.Equal("Executada", Etapa(envelope, 6).Estado);
        Assert.Equal(FluxoNoveEtapas.JustificativaFerramentasExternasConsultadas, Etapa(envelope, 6).Justificativa);
    }
}
