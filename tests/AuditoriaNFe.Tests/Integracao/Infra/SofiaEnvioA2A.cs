using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Agente.A2A;
using Api.Agente.A2A.Sofia;

namespace AuditoriaNFe.Tests.Integracao.Infra;

/// <summary>Envio JSON-RPC v0.3 <c>message/send</c> com contrato SOFIA na única parte de texto.</summary>
internal static class SofiaEnvioA2A
{
    public static async Task<string> EnviarSolicitacaoAsync(
        HttpClient cliente,
        SolicitacaoAnaliseSofia solicitacao,
        CancellationToken cancelamento)
    {
        var texto = JsonSerializer.Serialize(solicitacao, ContratoSofiaV1.Json);
        using var resposta = await EnviarTextoAsync(cliente, texto, cancelamento);
        return await resposta.Content.ReadAsStringAsync(cancelamento);
    }

    public static Task<HttpResponseMessage> EnviarTextoAsync(HttpClient cliente, string textoContrato, CancellationToken cancelamento)
    {
        var textoEscapado = JsonSerializer.Serialize(textoContrato);
        var corpo = "{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"message/send\",\"params\":{\"message\":{\"kind\":\"message\",\"role\":\"user\",\"parts\":[{\"kind\":\"text\",\"text\":" + textoEscapado + "}]}}}";
        return cliente.PostAsync(
            FabricaAgentCard.RotaA2A,
            new StringContent(corpo, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
            cancelamento);
    }
}
