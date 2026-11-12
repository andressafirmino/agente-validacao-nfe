using System.Security.Cryptography;
using System.Text;
using Api.RAG.Configuracao;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Api.RAG.Seguranca;

public static class ProtecaoAdministracaoRagExtensions
{
    public const string PrefixoApi = "/api/v1/rag";

    public static IServiceCollection AdicionarProtecaoAdministracaoRag(this IServiceCollection services) =>
        services.AddSingleton<ValidadorChaveAdministracaoRag>();

    public static IApplicationBuilder UsarProtecaoAdministracaoRag(this IApplicationBuilder app) =>
        app.UseMiddleware<ProtecaoAdministracaoRag>();
}

public sealed class ValidadorChaveAdministracaoRag(IOptions<RagOpcoes> opcoes)
{
    private readonly byte[]? _resumoChave = string.IsNullOrWhiteSpace(opcoes.Value.ChaveAdministracao)
        ? null
        : SHA256.HashData(Encoding.UTF8.GetBytes(opcoes.Value.ChaveAdministracao));

    public bool Configurada => _resumoChave is not null;

    public bool Autenticada(HttpRequest requisicao)
    {
        if (_resumoChave is null)
        {
            return false;
        }

        var cabecalhos = requisicao.Headers[HeaderNames.Authorization];
        if (cabecalhos.Count != 1 || cabecalhos[0] is not { } valor)
        {
            return false;
        }

        var separador = valor.IndexOf(' ', StringComparison.Ordinal);
        if (separador <= 0
            || !valor.AsSpan(0, separador).Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = valor[(separador + 1)..].Trim();
        if (token.Length == 0)
        {
            return false;
        }

        var tentativa = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(_resumoChave, tentativa);
    }
}

public sealed class ProtecaoAdministracaoRag(RequestDelegate proximo, ValidadorChaveAdministracaoRag validador)
{
    public Task InvokeAsync(HttpContext contexto)
    {
        if (!contexto.Request.Path.StartsWithSegments(ProtecaoAdministracaoRagExtensions.PrefixoApi))
        {
            return proximo(contexto);
        }

        if (HttpMethods.IsGet(contexto.Request.Method)
            && contexto.Request.Path.Value?.Equals("/api/v1/rag/ingestao", StringComparison.OrdinalIgnoreCase) == true)
        {
            return proximo(contexto);
        }

        if (!validador.Configurada)
        {
            contexto.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Task.CompletedTask;
        }

        if (!validador.Autenticada(contexto.Request))
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            contexto.Response.Headers.WWWAuthenticate = "Bearer";
            return Task.CompletedTask;
        }

        return proximo(contexto);
    }
}
