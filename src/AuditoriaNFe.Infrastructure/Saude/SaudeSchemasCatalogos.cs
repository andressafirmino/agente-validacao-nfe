using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Infrastructure.Schemas;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AuditoriaNFe.Infrastructure.Saude;

/// <summary>
/// Manifesto de schemas e catálogos carregados e válidos; o schema do pacote mais recente compila.
/// A resposta não revela caminhos nem detalhes da falha.
/// </summary>
public sealed class SaudeSchemasCatalogos(IServiceProvider servicos) : IHealthCheck
{
    public const string Nome = "schemas-catalogos";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var provedor = (ProvedorPacotesSchema)(servicos.GetService(typeof(ProvedorPacotesSchema)) ?? throw new InvalidOperationException());
            var catalogo = (ICatalogoRegras)(servicos.GetService(typeof(ICatalogoRegras)) ?? throw new InvalidOperationException());
            provedor.Esquemas(provedor.Vigencias[^1].Id);
            return Task.FromResult(catalogo.Regras.Count > 0
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, "Catálogo de regras vazio."));
        }
        catch (Exception erro) when (erro is not OutOfMemoryException)
        {
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, "Schemas ou catálogos inválidos."));
        }
    }
}
