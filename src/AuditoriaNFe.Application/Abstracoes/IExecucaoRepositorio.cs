using AuditoriaNFe.Domain.Sessoes;

namespace AuditoriaNFe.Application.Abstracoes;

/// <summary>Execuções no armazenamento (FR-054, NFR-008), sempre filtradas pela chave de isolamento.</summary>
public interface IExecucaoRepositorio
{
    Task<Execucao?> ObterAsync(Guid execucaoId, string chaveIsolamento, CancellationToken cancellationToken);

    /// <summary>Execuções da sessão, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<Execucao>> ListarPorSessaoAsync(Guid sessaoId, string chaveIsolamento, CancellationToken cancellationToken);

    Task InserirAsync(Execucao execucao, CancellationToken cancellationToken);

    /// <summary>Busca a execução ativa ou concluída com a mesma chave de idempotência SOFIA (FR-043h).</summary>
    Task<Execucao?> ObterPorChaveIdempotenciaSofiaAsync(string chaveIdempotencia, CancellationToken cancellationToken);

    Task AtualizarAsync(Execucao execucao, CancellationToken cancellationToken);
}
