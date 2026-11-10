using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Domain.ContextoUsuario;
using AuditoriaNFe.Domain.Sessoes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Application.Servicos;

/// <summary>
/// Atualização do contexto do usuário na etapa 9 a partir dos resultados determinísticos (FR-055a,
/// FR-055b, FR-057): só para usuário identificado, idempotente por execução. A saída da IA não chega
/// aqui. Falha do armazenamento não derruba a execução: o parecer é entregue com
/// <c>contextoUsuarioAtualizado</c> falso.
/// </summary>
public sealed partial class AtualizadorContextoUsuario(
    IContextoUsuarioRepositorio contextos,
    IRelogio relogio,
    IOptions<ContextoUsuarioOpcoes> opcoes,
    ILogger<AtualizadorContextoUsuario> logger)
{
    public async Task AtualizarAsync(Execucao execucao, ContribuicaoExecucao contribuicao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execucao);
        ArgumentNullException.ThrowIfNull(contribuicao);
        if (!execucao.UsuarioIdentificado)
        {
            return;
        }

        var limites = GravacaoContextoUsuario.Limites(opcoes.Value);
        try
        {
            await GravacaoContextoUsuario.AlterarAsync(
                contextos,
                relogio,
                execucao.ChaveIsolamento,
                agora => ContextoUsuario.Criar(execucao.ChaveIsolamento, execucao.UsuarioId, execucao.ChamadorId, agora, limites),
                (contexto, agora) => contexto.ContabilizarExecucao(contribuicao, agora, limites),
                cancellationToken);
            execucao.MarcarContextoUsuarioAtualizado();
        }
        catch (Exception erro) when (erro is ArmazenamentoIndisponivelException or InvalidOperationException)
        {
            LogFalhaContexto(logger, execucao.Id, erro.GetType().Name);
        }
    }

    [LoggerMessage(EventId = 1535, Level = LogLevel.Warning,
        Message = "Contexto do usuário não atualizado na execução {ExecucaoId} ({TipoErro}).")]
    private static partial void LogFalhaContexto(ILogger logger, Guid execucaoId, string tipoErro);
}
