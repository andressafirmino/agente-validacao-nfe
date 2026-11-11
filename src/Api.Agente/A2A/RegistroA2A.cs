using System.Text.Json;
using A2A.V0_3Compat;
using Api.Agente.A2A.Sofia;
using Api.Agente.Limites;
using global::A2A;

namespace Api.Agente.A2A;

public static class RegistroA2A
{
    public static IServiceCollection AdicionarServidorA2A(this IServiceCollection services)
    {
        services.AddSingleton<ITaskStore, InMemoryTaskStore>();
        services.AddSingleton<ChannelEventNotifier>();
        services.AddSingleton<ValidadorSolicitacaoSofia>();
        services.AddSingleton<MapeadorSolicitacaoSofia>();
        services.AddSingleton<MapeadorResultadoSofia>();
        services.AddSingleton<IAgentHandler, ManipuladorSofiaA2A>();
        services.AddSingleton<IA2ARequestHandler>(sp => new A2AServer(
            sp.GetRequiredService<IAgentHandler>(),
            sp.GetRequiredService<ITaskStore>(),
            sp.GetRequiredService<ChannelEventNotifier>(),
            sp.GetRequiredService<ILogger<A2AServer>>(),
            new A2AServerOptions { SupportsStreaming = false }));

        return services;
    }

    public static WebApplication MapearA2A(this WebApplication app)
    {
        var handler = app.Services.GetRequiredService<IA2ARequestHandler>();
        var fabrica = app.Services.GetRequiredService<FabricaAgentCard>();

        app.MapA2AWithV03Compat(handler, FabricaAgentCard.RotaA2A)
            .WithMetadata(LimiteConcorrenciaExecucao.Instancia)
            .RequireRateLimiting(LimitesHttpExtensions.PoliticaTaxa);

        app.MapAgentCardGetWithV03Compat(
            handler,
            () => Task.FromResult(fabrica.CriarCardSdk()),
            FabricaAgentCard.Rota,
            blendedCard: false);

        return app;
    }
}
