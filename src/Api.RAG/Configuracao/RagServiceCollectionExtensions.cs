using Api.RAG.Infraestrutura;
using Api.RAG.Infraestrutura.Embeddings;
using Microsoft.EntityFrameworkCore;
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

    public static IServiceCollection AdicionarPersistenciaRag(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(RagDbContext.NomeConnectionString)
            ?? throw new InvalidOperationException($"ConnectionStrings:{RagDbContext.NomeConnectionString} é obrigatória.");

        services.AddDbContextFactory<RagDbContext>((_, builder) =>
            RagDbContext.Configurar(builder, connectionString));

        services.AddSingleton<IProvedorEmbeddings, ProvedorEmbeddingsIndisponivel>();
        return services;
    }
}
