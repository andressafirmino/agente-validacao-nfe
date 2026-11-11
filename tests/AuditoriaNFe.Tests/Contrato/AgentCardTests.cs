using System.Text.Json;
using Api.Agente.A2A;
using Api.Agente.Seguranca;
using AuditoriaNFe.Tests.Integracao.Infra;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>TS-029, FR-044: Agent Card v0.3 e negociação 1.0 conforme contrato SOFIA.</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class AgentCardTests(AmbienteIntegracao ambiente)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Card_v03_TS029_contem_campos_do_contrato()
    {
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(ambiente.Fabrica);

        using var resposta = await cliente.GetAsync(FabricaAgentCard.Rota, Cancelamento);
        var json = await resposta.Content.ReadAsStringAsync(Cancelamento);

        Assert.Contains("\"protocolVersion\":\"0.3.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"preferredTransport\":\"JSONRPC\"", json, StringComparison.Ordinal);
        Assert.Contains("\"streaming\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"pushNotifications\":false", json, StringComparison.Ordinal);
        Assert.Contains("urn:infis:sofia:contrato", json, StringComparison.Ordinal);
        Assert.Contains("\"versaoContrato\":\"1.0\"", json, StringComparison.Ordinal);
        Assert.Contains("\"nomeArtifact\":\"analise-sofia-v1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"idAgente\":\"agente-auditoria-nfe\"", json, StringComparison.Ordinal);
        Assert.Contains("analisar_documentos", json, StringComparison.Ordinal);
        Assert.Contains("sofiaService", json, StringComparison.Ordinal);
        Assert.Contains("/a2a/auditoria-nfe", json, StringComparison.Ordinal);
        Assert.DoesNotContain(CredencialServicoSofiaTests.Chave, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Com_cabecalho_A2A_Version_1_0_retorna_card_no_formato_1_0()
    {
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(ambiente.Fabrica);
        cliente.DefaultRequestHeaders.TryAddWithoutValidation("A2A-Version", "1.0");

        using var resposta = await cliente.GetAsync(FabricaAgentCard.Rota, Cancelamento);
        var json = await resposta.Content.ReadAsStringAsync(Cancelamento);
        using var documento = JsonDocument.Parse(json);
        var raiz = documento.RootElement;

        Assert.True(raiz.TryGetProperty("supportedInterfaces", out _));
        Assert.False(raiz.TryGetProperty("protocolVersion", out _));
    }
}
