using Api.Agente.Hospedagem;
using Api.Agente.Seguranca;

namespace Api.Agente.A2A;

/// <summary>
/// Registro e rota do Agent Card nos modos que o publicam (FR-052f, FR-052g), protegidos pela
/// credencial de serviço da SOFIA (FR-052h).
/// </summary>
public static class AgentCardExtensions
{
    public static IServiceCollection AdicionarAgentCard(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<HospedagemOpcoes>()
            .Bind(configuration.GetSection(HospedagemOpcoes.Secao));
        services.AdicionarOpcoesA2A(configuration);
        services.AdicionarCredencialServicoSofia(configuration);
        services.AddSingleton<FabricaAgentCard>();

        return services;
    }

    public static IEndpointRouteBuilder MapearAgentCard(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet(FabricaAgentCard.Rota, (FabricaAgentCard fabrica) => Results.Json(fabrica.Card, FabricaAgentCard.OpcoesJson));

        return rotas;
    }
}
