using Api.Agente.A2A;
using Microsoft.AspNetCore.HttpOverrides;

namespace Api.Agente.Hospedagem;

/// <summary>
/// Acréscimos do modo <c>Homologacao</c> ao agente completo (FR-052g, desvio D-09, ADR-0013): Agent
/// Card com a implantação declarada e o esquema e o IP do cliente informados pelo proxy do Vercel,
/// que termina o TLS.
/// </summary>
public static class HomologacaoExtensions
{
    public static IServiceCollection AdicionarHomologacao(this IServiceCollection services, IConfiguration configuration)
    {
        services.AdicionarAgentCard(configuration);
        services.Configure<ForwardedHeadersOptions>(opcoes =>
        {
            opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;

            // O Vercel não publica os endereços do proxy. Com ForwardLimit = 1 (padrão), só o último
            // valor de cada cabeçalho, o acrescentado pela plataforma, é aplicado.
            opcoes.KnownIPNetworks.Clear();
            opcoes.KnownProxies.Clear();
        });

        return services;
    }

    public static WebApplication UsarCabecalhosDoProxy(this WebApplication app)
    {
        app.UseForwardedHeaders();

        return app;
    }
}
