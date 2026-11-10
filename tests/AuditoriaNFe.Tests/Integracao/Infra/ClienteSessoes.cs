using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AuditoriaNFe.Application.Contratos;

namespace AuditoriaNFe.Tests.Integracao.Infra;

/// <summary>Chamadas a <c>/api/v1/sessoes</c> e <c>/api/v1/contexto-usuario</c> como no contrato OpenAPI.</summary>
public static class ClienteSessoes
{
    public const string RotaContexto = "/api/v1/contexto-usuario";

    public static Task<RespostaExecucao> EncerrarAsync(
        HttpClient cliente,
        Guid sessaoId,
        string? usuarioId = ClienteExecucoes.UsuarioPadrao,
        string? chamadorId = ClienteExecucoes.ChamadorPadrao,
        CancellationToken cancelamento = default) =>
        EnviarAsync(cliente, HttpMethod.Delete, $"/api/v1/sessoes/{sessaoId}", usuarioId, chamadorId, null, cancelamento);

    public static Task<RespostaExecucao> ObterContextoAsync(
        HttpClient cliente,
        string? usuarioId,
        string? chamadorId = ClienteExecucoes.ChamadorPadrao,
        CancellationToken cancelamento = default) =>
        EnviarAsync(cliente, HttpMethod.Get, RotaContexto, usuarioId, chamadorId, null, cancelamento);

    public static Task<RespostaExecucao> ExcluirContextoAsync(
        HttpClient cliente,
        string? usuarioId,
        string? chamadorId = ClienteExecucoes.ChamadorPadrao,
        CancellationToken cancelamento = default) =>
        EnviarAsync(cliente, HttpMethod.Delete, RotaContexto, usuarioId, chamadorId, null, cancelamento);

    /// <summary><paramref name="corpoJson"/> vai como está, para testar também corpos fora do contrato.</summary>
    public static Task<RespostaExecucao> DeclararPreferenciasAsync(
        HttpClient cliente,
        string corpoJson,
        string? usuarioId,
        string? chamadorId = ClienteExecucoes.ChamadorPadrao,
        CancellationToken cancelamento = default) =>
        EnviarAsync(cliente, HttpMethod.Put, $"{RotaContexto}/preferencias", usuarioId, chamadorId, corpoJson, cancelamento);

    public static Task<RespostaExecucao> RemoverCnpjAsync(
        HttpClient cliente,
        string cnpj,
        string? papel,
        string? usuarioId,
        string? chamadorId = ClienteExecucoes.ChamadorPadrao,
        CancellationToken cancelamento = default) =>
        EnviarAsync(
            cliente,
            HttpMethod.Delete,
            papel is null ? $"{RotaContexto}/cnpjs/{cnpj}" : $"{RotaContexto}/cnpjs/{cnpj}?papel={papel}",
            usuarioId,
            chamadorId,
            null,
            cancelamento);

    public static Task<RespostaExecucao> RemoverAchadoRecorrenteAsync(
        HttpClient cliente,
        string chave,
        string? usuarioId,
        string? chamadorId = ClienteExecucoes.ChamadorPadrao,
        CancellationToken cancelamento = default) =>
        EnviarAsync(cliente, HttpMethod.Delete, $"{RotaContexto}/achados-recorrentes/{chave}", usuarioId, chamadorId, null, cancelamento);

    public static ContextoUsuarioV1 Contexto(RespostaExecucao resposta) =>
        JsonSerializer.Deserialize<ContextoUsuarioV1>(resposta.Corpo, OpcoesJsonContrato.Padrao)
            ?? throw new InvalidOperationException("Corpo sem ContextoUsuario.");

    private static Task<RespostaExecucao> EnviarAsync(
        HttpClient cliente,
        HttpMethod metodo,
        string rota,
        string? usuarioId,
        string? chamadorId,
        string? corpoJson,
        CancellationToken cancelamento)
    {
        var requisicao = new HttpRequestMessage(metodo, rota);
        if (corpoJson is not null)
        {
            requisicao.Content = new StringContent(corpoJson, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
        }

        ClienteExecucoes.Identificar(requisicao, usuarioId, chamadorId);
        return EnviarEDescartarAsync(cliente, requisicao, cancelamento);
    }

    private static async Task<RespostaExecucao> EnviarEDescartarAsync(HttpClient cliente, HttpRequestMessage requisicao, CancellationToken cancelamento)
    {
        using (requisicao)
        {
            return await ClienteExecucoes.EnviarAsync(cliente, requisicao, cancelamento);
        }
    }
}
