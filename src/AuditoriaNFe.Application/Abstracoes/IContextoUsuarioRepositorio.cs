using AuditoriaNFe.Domain.ContextoUsuario;

namespace AuditoriaNFe.Application.Abstracoes;

/// <summary>
/// Contexto do usuário no armazenamento (data-model §13), sempre pela chave de isolamento (FR-056).
/// Indisponibilidade gera <see cref="ArmazenamentoIndisponivelException"/>.
/// </summary>
public interface IContextoUsuarioRepositorio
{
    /// <summary>Ignora o contexto com <c>expiraEm</c> vencido, mesmo antes da remoção pelo TTL.</summary>
    Task<ContextoUsuario?> ObterAsync(string chaveIsolamento, DateTimeOffset agora, CancellationToken cancellationToken);

    /// <summary>
    /// Grava o contexto lido com <paramref name="atualizadoEmLido"/> (<see langword="null"/> para um
    /// contexto novo, que substitui um vencido). Devolve <see langword="false"/> quando outra gravação
    /// alterou o contexto desde a leitura.
    /// </summary>
    Task<bool> SalvarAsync(ContextoUsuario contexto, DateTimeOffset? atualizadoEmLido, CancellationToken cancellationToken);

    Task ExcluirAsync(string chaveIsolamento, CancellationToken cancellationToken);
}
