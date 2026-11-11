namespace AuditoriaNFe.Application.Abstracoes;

/// <summary>
/// Etapa 6 — capacidades de ferramentas externas via MCP (FR-047a). Na v1 só registra
/// disponibilidade; nenhuma ação de negócio é delegada a integrações externas.
/// </summary>
public interface IFerramentasExternas
{
    bool Habilitada { get; }

    Task<ResultadoFerramentasExternas> ConsultarCapacidadesAsync(CancellationToken cancellationToken);
}

public sealed record ResultadoFerramentasExternas(IReadOnlyList<string> Capacidades);

public sealed class FerramentasExternasIndisponivelException(string mensagem, Exception? interna = null)
    : Exception(mensagem, interna);
