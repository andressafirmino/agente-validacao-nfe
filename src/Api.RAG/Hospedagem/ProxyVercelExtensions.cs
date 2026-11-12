using Microsoft.AspNetCore.HttpOverrides;

namespace Api.RAG.Hospedagem;

/// <summary>Cabeçalhos X-Forwarded-* do Vercel para HTTPS e URLs corretas no OpenAPI/Swagger.</summary>
public static class ProxyVercelExtensions
{
    public static IServiceCollection AdicionarProxyVercel(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(opcoes =>
        {
            opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
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
