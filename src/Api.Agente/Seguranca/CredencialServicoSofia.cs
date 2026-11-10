using System.Security.Cryptography;
using System.Text;
using Api.Agente.A2A;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Api.Agente.Seguranca;

/// <summary>
/// Credencial de serviço da SOFIA (FR-052h, ADR-0014): Bearer opaco compartilhado, lido da variável
/// <c>SOFIA_A2A_TOKEN</c>. Sem valor, o agente não inicia; nunca há acesso livre por falta de configuração.
/// </summary>
public sealed class CredencialServicoSofiaOpcoes
{
    public const string ChaveToken = "SOFIA_A2A_TOKEN";
    public const string NomeEsquema = "sofiaService";

    public string Token { get; set; } = string.Empty;
}

public static class CredencialServicoSofiaExtensions
{
    public const string PrefixoA2A = "/a2a";

    public static IServiceCollection AdicionarCredencialServicoSofia(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CredencialServicoSofiaOpcoes>()
            .Configure(o => o.Token = configuration[CredencialServicoSofiaOpcoes.ChaveToken] ?? string.Empty)
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Token),
                $"{CredencialServicoSofiaOpcoes.ChaveToken} deve ser configurada: o Agent Card e as rotas A2A exigem a credencial de serviço da SOFIA (FR-052h).")
            .Validate(
                o => o.Token.Length == o.Token.Trim().Length,
                $"{CredencialServicoSofiaOpcoes.ChaveToken} não pode ter espaços ou quebras de linha nas extremidades (FR-052h).")
            .ValidateOnStart();

        return services;
    }

    /// <summary>Exige a credencial no card e em toda rota sob <c>/a2a</c>, mapeada ou não.</summary>
    public static IApplicationBuilder UsarCredencialServicoSofia(this IApplicationBuilder app) =>
        app.UseMiddleware<ProtecaoCredencialServicoSofia>();
}

/// <summary>
/// Responde 401 com <c>WWW-Authenticate: Bearer</c> antes de qualquer processamento quando o
/// cabeçalho falta, está malformado ou traz outra chave. Aceita a credencial só no cabeçalho
/// <c>Authorization</c>; o esquema não diferencia maiúsculas, o token sim.
/// </summary>
public sealed class ProtecaoCredencialServicoSofia(RequestDelegate proximo, IOptions<CredencialServicoSofiaOpcoes> opcoes)
{
    private const string Esquema = "Bearer";

    // Comparar resumos de tamanho fixo não revela o tamanho da chave configurada.
    private readonly byte[] _resumoChave = SHA256.HashData(Encoding.UTF8.GetBytes(opcoes.Value.Token));

    public Task InvokeAsync(HttpContext contexto)
    {
        if (!Protegida(contexto.Request.Path) || Autenticada(contexto.Request))
        {
            return proximo(contexto);
        }

        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
        contexto.Response.Headers.WWWAuthenticate = Esquema;
        return Task.CompletedTask;
    }

    private static bool Protegida(PathString caminho) =>
        caminho.StartsWithSegments(FabricaAgentCard.Rota) || caminho.StartsWithSegments(CredencialServicoSofiaExtensions.PrefixoA2A);

    private bool Autenticada(HttpRequest requisicao)
    {
        var cabecalhos = requisicao.Headers[HeaderNames.Authorization];
        if (cabecalhos.Count != 1 || cabecalhos[0] is not { } valor)
        {
            return false;
        }

        var separador = valor.IndexOf(' ', StringComparison.Ordinal);
        if (separador <= 0 || !valor.AsSpan(0, separador).Equals(Esquema, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var chave = valor.AsSpan(separador + 1).TrimStart(' ');
        if (chave.IsEmpty)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(chave.ToString())), _resumoChave);
    }
}
