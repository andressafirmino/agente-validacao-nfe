using Api.RAG.Infraestrutura;
using Microsoft.EntityFrameworkCore;

namespace Api.RAG.Endpoints;

public static class SaudeRagEndpoints
{
    public static void Mapear(WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok("Healthy"));

        app.MapGet("/health/ready", async (IDbContextFactory<RagDbContext> fabrica, CancellationToken cancelamento) =>
        {
            try
            {
                await using var contexto = await fabrica.CreateDbContextAsync(cancelamento).ConfigureAwait(false);
                if (!await contexto.Database.CanConnectAsync(cancelamento).ConfigureAwait(false))
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }

                return Results.Ok("Healthy");
            }
            catch
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        });
    }
}
