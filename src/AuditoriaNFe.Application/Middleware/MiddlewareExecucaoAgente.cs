using System.Diagnostics;
using System.Runtime.CompilerServices;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Consumo;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Application.Middleware;

/// <summary>
/// Middleware de execução do agente interno (research R-09): limita cada chamada a
/// <c>Ia:TimeoutChamada</c>, converte exceções do provedor em
/// <see cref="ProvedorIaIndisponivelException"/> (ERR-014) e registra início e fim com a execução
/// e a tentativa, sem o conteúdo das mensagens. O cancelamento de quem chamou (tempo máximo da
/// execução, ERR-016) segue como <see cref="OperationCanceledException"/>.
/// </summary>
public sealed partial class MiddlewareExecucaoAgente(IOptions<IaOpcoes> ia, ILogger<MiddlewareExecucaoAgente> logger)
{
    public AIAgent Aplicar(AIAgent agente)
    {
        ArgumentNullException.ThrowIfNull(agente);
        return agente.AsBuilder().Use(runFunc: ExecutarAsync, runStreamingFunc: ExecutarStreamingAsync).Build();
    }

    private async Task<AgentResponse> ExecutarAsync(
        IEnumerable<ChatMessage> mensagens,
        AgentSession? sessao,
        AgentRunOptions? opcoes,
        AIAgent interno,
        CancellationToken cancellationToken)
    {
        var escopo = EscopoConsumo.Atual;
        var inicio = Stopwatch.GetTimestamp();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(ia.Value.TimeoutChamada);
        LogInicio(logger, escopo?.ExecucaoId, escopo?.Tentativa);
        try
        {
            var resposta = await interno.RunAsync(mensagens, sessao, opcoes, limite.Token).ConfigureAwait(false);
            LogFim(logger, escopo?.ExecucaoId, escopo?.Tentativa, (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
            return resposta;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception erro) when (erro is not ProvedorIaIndisponivelException and not OutOfMemoryException)
        {
            LogFalha(logger, escopo?.ExecucaoId, escopo?.Tentativa, erro.GetType().Name);
            throw new ProvedorIaIndisponivelException("O provedor de IA não respondeu.", erro);
        }
    }

    private async IAsyncEnumerable<AgentResponseUpdate> ExecutarStreamingAsync(
        IEnumerable<ChatMessage> mensagens,
        AgentSession? sessao,
        AgentRunOptions? opcoes,
        AIAgent interno,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var escopo = EscopoConsumo.Atual;
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(ia.Value.TimeoutChamada);
        LogInicio(logger, escopo?.ExecucaoId, escopo?.Tentativa);
        await foreach (var atualizacao in interno.RunStreamingAsync(mensagens, sessao, opcoes, limite.Token).ConfigureAwait(false))
        {
            yield return atualizacao;
        }
    }

    [LoggerMessage(EventId = 1501, Level = LogLevel.Information,
        Message = "Agente interno chamado na execução {ExecucaoId}, tentativa {Tentativa}.")]
    private static partial void LogInicio(ILogger logger, Guid? execucaoId, int? tentativa);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Information,
        Message = "Agente interno respondeu na execução {ExecucaoId}, tentativa {Tentativa}, em {DuracaoMs} ms.")]
    private static partial void LogFim(ILogger logger, Guid? execucaoId, int? tentativa, long duracaoMs);

    [LoggerMessage(EventId = 1503, Level = LogLevel.Warning,
        Message = "Provedor de IA indisponível na execução {ExecucaoId}, tentativa {Tentativa} ({TipoErro}).")]
    private static partial void LogFalha(ILogger logger, Guid? execucaoId, int? tentativa, string tipoErro);
}
