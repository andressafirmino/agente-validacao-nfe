using AuditoriaNFe.Application.Abstracoes;

namespace AuditoriaNFe.Infrastructure.Mcp;

/// <summary><c>Mcp:Habilitado = false</c>: não chama nada; a etapa 5 fica "não aplicável — integração não habilitada" (FR-047b).</summary>
public sealed class ConsultaManuaisDesabilitada : IConsultaManuais
{
    public bool Habilitada => false;

    public Task<ResultadoConsultaManuais> ConsultarAsync(ConsultaManuais consulta, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A consulta a manuais não está habilitada.");
}
