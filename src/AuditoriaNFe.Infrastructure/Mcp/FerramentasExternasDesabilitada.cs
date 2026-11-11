using AuditoriaNFe.Application.Abstracoes;

namespace AuditoriaNFe.Infrastructure.Mcp;

public sealed class FerramentasExternasDesabilitada : IFerramentasExternas
{
    public bool Habilitada => false;

    public Task<ResultadoFerramentasExternas> ConsultarCapacidadesAsync(CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A etapa de ferramentas externas não está habilitada.");
}
