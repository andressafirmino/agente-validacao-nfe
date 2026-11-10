using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Servicos;
using AuditoriaNFe.Domain.ContextoUsuario;
using AuditoriaNFe.Domain.Identificacao;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Application.CasosDeUso;

/// <summary>
/// Consulta, correção e exclusão do contexto pelo próprio usuário no canal direto (FR-059), sempre
/// pela chave de isolamento (FR-056). Cada declaração ou correção renova <c>expiraEm</c> (FR-060).
/// "anonimo" não tem contexto (FR-052c): a consulta não encontra nada e as alterações são ignoradas.
/// </summary>
public sealed class GerirContextoUsuario(
    IContextoUsuarioRepositorio contextos,
    IRelogio relogio,
    IOptions<ContextoUsuarioOpcoes> opcoes)
{
    public Task<ContextoUsuario?> ObterAsync(IdentificacaoSolicitante identificacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        return identificacao.UsuarioIdentificado
            ? contextos.ObterAsync(identificacao.ChaveIsolamento, relogio.Agora, cancellationToken)
            : Task.FromResult<ContextoUsuario?>(null);
    }

    /// <summary>Cria o contexto quando ainda não existe. Valores fora das listas lançam <see cref="ArgumentException"/>.</summary>
    public async Task<ContextoUsuario> DeclararPreferenciasAsync(
        IdentificacaoSolicitante identificacao,
        NivelDetalhe nivelDetalhe,
        IReadOnlyCollection<string> tributosInteresse,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        if (!identificacao.UsuarioIdentificado)
        {
            throw new InvalidOperationException("Usuário não identificado não tem contexto (FR-052c).");
        }

        var limites = GravacaoContextoUsuario.Limites(opcoes.Value);
        var gravado = await GravacaoContextoUsuario.AlterarAsync(
            contextos,
            relogio,
            identificacao.ChaveIsolamento,
            agora => ContextoUsuario.Criar(identificacao.ChaveIsolamento, identificacao.UsuarioId, identificacao.ChamadorId, agora, limites),
            (contexto, agora) =>
            {
                contexto.DeclararPreferencias(nivelDetalhe, tributosInteresse, agora, limites);
                return true;
            },
            cancellationToken);
        return gravado!;
    }

    /// <summary>Sem <paramref name="papel"/>, remove o CNPJ nos dois papéis; CNPJ inexistente não é erro.</summary>
    public Task RemoverCnpjAsync(IdentificacaoSolicitante identificacao, string cnpj, PapelCnpj? papel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        var limites = GravacaoContextoUsuario.Limites(opcoes.Value);
        return AlterarExistenteAsync(identificacao, (contexto, agora) => contexto.RemoverCnpj(cnpj, papel, agora, limites), cancellationToken);
    }

    public Task RemoverAchadoRecorrenteAsync(IdentificacaoSolicitante identificacao, string chave, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        var limites = GravacaoContextoUsuario.Limites(opcoes.Value);
        return AlterarExistenteAsync(identificacao, (contexto, agora) => contexto.RemoverAchadoRecorrente(chave, agora, limites), cancellationToken);
    }

    /// <summary>As execuções seguintes começam sem contexto (TS-056).</summary>
    public Task ExcluirAsync(IdentificacaoSolicitante identificacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        return identificacao.UsuarioIdentificado
            ? contextos.ExcluirAsync(identificacao.ChaveIsolamento, cancellationToken)
            : Task.CompletedTask;
    }

    private async Task AlterarExistenteAsync(
        IdentificacaoSolicitante identificacao,
        Func<ContextoUsuario, DateTimeOffset, bool> alterar,
        CancellationToken cancellationToken)
    {
        if (identificacao.UsuarioIdentificado)
        {
            await GravacaoContextoUsuario.AlterarAsync(contextos, relogio, identificacao.ChaveIsolamento, criar: null, alterar, cancellationToken);
        }
    }
}
