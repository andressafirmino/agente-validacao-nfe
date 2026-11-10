using Api.Agente.A2A;
using AuditoriaNFe.Application.Configuracao;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Api.Agente.Hospedagem;

/// <summary>
/// Modo <c>SomenteAgentCard</c> (FR-052f, D-08): só o card e a vivacidade. Não registra persistência,
/// IA, documentação, CORS, limites nem rotas de análise; qualquer outra rota responde 404.
/// </summary>
public static class SomenteAgentCardExtensions
{
    public const string RotaVivacidade = "/health/live";

    public static IServiceCollection AdicionarSomenteAgentCard(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LimitesOpcoes>()
            .Bind(configuration.GetSection(LimitesOpcoes.Secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AdicionarAgentCard(configuration);
        services.AddHealthChecks();

        return services;
    }

    public static WebApplication MapearSomenteAgentCard(this WebApplication app)
    {
        app.MapearAgentCard();
        app.MapHealthChecks(RotaVivacidade, new HealthCheckOptions { Predicate = _ => false });

        return app;
    }
}
