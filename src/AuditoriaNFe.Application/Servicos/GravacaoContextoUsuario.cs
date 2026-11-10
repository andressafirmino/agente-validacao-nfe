using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Domain.ContextoUsuario;

namespace AuditoriaNFe.Application.Servicos;

/// <summary>
/// Leitura, alteração e gravação do contexto do usuário com nova leitura quando outra gravação
/// concorrente alterou o contexto desde a leitura.
/// </summary>
internal static class GravacaoContextoUsuario
{
    public const int MaxTentativas = 3;

    public static LimitesContextoUsuario Limites(ContextoUsuarioOpcoes opcoes) =>
        new(opcoes.RetencaoInatividade, opcoes.MaxCnpjs, opcoes.MaxAchados);

    /// <summary>
    /// <paramref name="alterar"/> devolve se houve mudança. Sem contexto vigente, usa
    /// <paramref name="criar"/>; sem <paramref name="criar"/>, nada é gravado e o retorno é nulo.
    /// </summary>
    public static async Task<ContextoUsuario?> AlterarAsync(
        IContextoUsuarioRepositorio contextos,
        IRelogio relogio,
        string chaveIsolamento,
        Func<DateTimeOffset, ContextoUsuario>? criar,
        Func<ContextoUsuario, DateTimeOffset, bool> alterar,
        CancellationToken cancellationToken)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            var agora = relogio.Agora;
            var existente = await contextos.ObterAsync(chaveIsolamento, agora, cancellationToken);
            var atualizadoEmLido = existente?.AtualizadoEm;
            var contexto = existente ?? criar?.Invoke(agora);
            if (contexto is null)
            {
                return null;
            }

            if (!alterar(contexto, agora) && existente is not null)
            {
                return contexto;
            }

            if (await contextos.SalvarAsync(contexto, atualizadoEmLido, cancellationToken))
            {
                return contexto;
            }

            if (tentativa >= MaxTentativas)
            {
                throw new InvalidOperationException("O contexto do usuário foi alterado por outra gravação em todas as tentativas.");
            }
        }
    }
}
