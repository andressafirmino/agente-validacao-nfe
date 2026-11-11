using Api.RAG.Configuracao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.RAG.Infraestrutura;

/// <summary>Aplica migrações do schema <c>rag</c> quando <c>Rag:AplicarMigracoes=true</c>.</summary>
public static class MigracoesRag
{
    public static async Task AplicarSeHabilitadoAsync(IServiceProvider servicos, CancellationToken cancellationToken = default)
    {
        if (!servicos.GetRequiredService<IOptions<RagOpcoes>>().Value.AplicarMigracoes)
        {
            return;
        }

        var fabrica = servicos.GetRequiredService<IDbContextFactory<RagDbContext>>();
        await using var contexto = await fabrica.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await contexto.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
