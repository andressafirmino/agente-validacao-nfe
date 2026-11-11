using System.Net.Http.Headers;
using AuditoriaNFe.Tests.Contrato;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Integracao.Infra;
using Sofia.Simulator;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>TS-035 e TS-035a pelo Sofia.Simulator (T152).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class SimuladorTests(AmbienteIntegracao ambiente)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TS035_fluxo_basico_exibe_card_resultado_e_confere_ids()
    {
        ambiente.ChatClient.Limpar().Responder(RespostasIa.Valida());
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(ambiente.Fabrica);
        var endereco = cliente.BaseAddress ?? throw new InvalidOperationException("BaseAddress ausente.");
        var saida = new StringWriter();
        var erro = new StringWriter();

        var codigo = await ExecutorSimulador.ExecutarAsync(endereco, "basico", cliente, saida, erro, Cancelamento);
        var texto = saida.ToString();

        Assert.Equal(0, codigo);
        Assert.Empty(erro.ToString());
        Assert.Contains("analisar_documentos", texto, StringComparison.Ordinal);
        Assert.Contains("agente-auditoria-nfe", texto, StringComparison.Ordinal);
        Assert.Contains("idAgente", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("=== Solicitação enviada (message/send) ===", texto, StringComparison.Ordinal);
        Assert.Contains("Estado da Task:", texto, StringComparison.Ordinal);
        Assert.Contains("Status SOFIA:", texto, StringComparison.Ordinal);
        Assert.Contains("Achados:", texto, StringComparison.Ordinal);
        Assert.Contains("Observações:", texto, StringComparison.Ordinal);
        Assert.Contains("Conferência dos IDs: os valores devolvidos coincidem com os enviados.", texto, StringComparison.Ordinal);
        Assert.Single(ambiente.ChatClient.Chamadas);
    }

    [Fact]
    public async Task TS035a_card_inacessivel_nao_envia_solicitacao()
    {
        ambiente.ChatClient.Limpar();
        using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CredencialServicoSofiaTests.Chave);
        var endereco = new Uri("http://127.0.0.1:51999");
        var saida = new StringWriter();
        var erro = new StringWriter();

        var codigo = await ExecutorSimulador.ExecutarAsync(endereco, "basico", cliente, saida, erro, Cancelamento);

        Assert.NotEqual(0, codigo);
        Assert.Contains("Falha de descoberta", erro.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Solicitação enviada", saida.ToString(), StringComparison.Ordinal);
        Assert.Empty(ambiente.ChatClient.Chamadas);
    }
}
