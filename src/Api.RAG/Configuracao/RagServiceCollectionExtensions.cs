using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.RAG.Configuracao;

public static class RagServiceCollectionExtensions
{
    public static IServiceCollection AdicionarOpcoesRag(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RagOpcoes>()
            .Bind(configuration.GetSection(RagOpcoes.Secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }
}
