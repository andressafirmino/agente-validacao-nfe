using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Sessoes;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Application.Sessoes;

/// <summary>Sessão obtida para uma solicitação ou o erro que a recusa (ERR-005 ou ERR-006).</summary>
public sealed record ObtencaoSessao(Sessao? Sessao, string? CodigoErro, string? Mensagem)
{
    public static ObtencaoSessao Ok(Sessao sessao) => new(sessao, null, null);

    public static ObtencaoSessao Falha(string codigo, string? mensagem = null) => new(null, codigo, mensagem);
}

/// <summary>
/// Criação, continuidade e encerramento de sessões (FR-036, FR-039 a FR-041). A chave de isolamento
/// é conferida em toda leitura: sessão de outro usuário ou chamador é inexistente (ERR-005), sem
/// revelar que existe. Expirada por <c>Sessao:ExpiracaoInatividade</c> ou encerrada responde ERR-006,
/// cada uma com a sua mensagem.
/// </summary>
public sealed class ServicoSessoes(ISessaoRepositorio sessoes, IGeradorIds ids, IOptions<SessaoOpcoes> opcoes)
{
    /// <summary>
    /// Sem <paramref name="sessaoId"/>, cria e grava uma sessão nova. A atividade da sessão aceita é
    /// renovada pelo caso de uso quando a solicitação é aceita.
    /// </summary>
    public async Task<ObtencaoSessao> ObterOuCriarAsync(
        Guid? sessaoId,
        IdentificacaoSolicitante identificacao,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        if (sessaoId is not { } id)
        {
            var nova = Sessao.Criar(ids.Novo(), identificacao, opcoes.Value.ExpiracaoInatividade, agora);
            await sessoes.InserirAsync(nova, cancellationToken);
            return ObtencaoSessao.Ok(nova);
        }

        var existente = await sessoes.ObterAsync(id, identificacao.ChaveIsolamento, cancellationToken);
        if (existente is null)
        {
            return ObtencaoSessao.Falha(CodigoErro.SessaoNaoEncontrada);
        }

        switch (existente.EstadoEm(agora))
        {
            case EstadoSessao.Ativa:
                return ObtencaoSessao.Ok(existente);
            case EstadoSessao.Expirada:
                if (existente.AtualizarExpiracao(agora))
                {
                    await sessoes.AtualizarAsync(existente, cancellationToken);
                }

                return ObtencaoSessao.Falha(CodigoErro.SessaoExpirada);
            default:
                return ObtencaoSessao.Falha(CodigoErro.SessaoExpirada, MensagensErro.SessaoEncerrada);
        }
    }

    /// <summary>
    /// Encerramento pelo usuário (FR-041). Devolve <see langword="false"/> quando a sessão não existe
    /// para esta identificação; sessão já encerrada ou expirada continua indisponível (idempotente).
    /// </summary>
    public async Task<bool> EncerrarAsync(
        Guid sessaoId,
        IdentificacaoSolicitante identificacao,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        var sessao = await sessoes.ObterAsync(sessaoId, identificacao.ChaveIsolamento, cancellationToken);
        if (sessao is null)
        {
            return false;
        }

        switch (sessao.EstadoEm(agora))
        {
            case EstadoSessao.Ativa:
                sessao.Encerrar(agora);
                await sessoes.AtualizarAsync(sessao, cancellationToken);
                break;
            case EstadoSessao.Expirada when sessao.AtualizarExpiracao(agora):
                await sessoes.AtualizarAsync(sessao, cancellationToken);
                break;
        }

        return true;
    }
}
