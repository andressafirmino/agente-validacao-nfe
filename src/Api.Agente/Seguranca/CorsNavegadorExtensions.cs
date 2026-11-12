using Api.Agente.Identificacao;
using Microsoft.Net.Http.Headers;

namespace Api.Agente.Seguranca;

/// <summary>
/// Política CORS <c>SofiaBrowser</c> (FR-052e, research R-34): só para navegadores das origens de
/// <c>Cors:AllowedOrigins</c> e só nas rotas do grupo <c>/api/v1</c>. A integração SOFIA → agente é de
/// servidor para servidor e não depende de CORS. Lista vazia = nenhuma origem permitida.
/// </summary>
public static class CorsNavegadorExtensions
{
    public const string Politica = "SofiaBrowser";
    public const string Secao = "Cors:AllowedOrigins";

    private static readonly TimeSpan CachePreflight = TimeSpan.FromMinutes(10);

    public static IServiceCollection AdicionarCorsNavegador(this IServiceCollection services, IConfiguration configuration)
    {
        var origens = LerOrigens(configuration);
        services.AddCors(opcoes => opcoes.AddPolicy(Politica, politica => politica
            .WithOrigins(origens)
            .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Delete)
            .WithHeaders(
                HeaderNames.ContentType,
                LeitorIdentificacaoHttp.CabecalhoUsuario,
                LeitorIdentificacaoHttp.CabecalhoUsuarioNome,
                LeitorIdentificacaoHttp.CabecalhoChamador)
            .WithExposedHeaders(HeaderNames.RetryAfter)
            .SetPreflightMaxAge(CachePreflight)));

        return services;
    }

    /// <summary>
    /// Origens no formato do cabeçalho <c>Origin</c>: http ou https, host e porta, sem caminho, barra
    /// final, curinga ou credenciais. Entradas vazias são ignoradas; origem inválida impede o início.
    /// </summary>
    public static string[] LerOrigens(IConfiguration configuration) =>
        (configuration.GetSection(Secao).Get<string[]>() ?? [])
            .Where(origem => !string.IsNullOrWhiteSpace(origem))
            .Select(origem => Validar(origem.Trim()))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string Validar(string origem)
    {
        var valida = Uri.TryCreate(origem, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.Equals(uri.GetLeftPart(UriPartial.Authority), origem, StringComparison.Ordinal);

        return valida
            ? origem
            : throw new InvalidOperationException(
                $"{Secao} contém uma origem inválida: '{origem}'. Use esquema http ou https, host e porta, sem caminho, barra final ou curinga (FR-052e).");
    }
}
