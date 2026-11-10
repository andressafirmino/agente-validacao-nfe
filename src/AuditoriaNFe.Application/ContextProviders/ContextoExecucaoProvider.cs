using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AuditoriaNFe.Application.ContextProviders;

/// <summary>
/// Entrega ao agente interno o contexto da execução como conteúdo delimitado e não confiável, numa
/// mensagem de usuário e nunca nas instruções de sistema (P07, FR-053; research R-07). É entregue a
/// cada chamada e não entra no histórico da sessão: o histórico guarda só as mensagens externas.
/// A pergunta do usuário vai na mensagem de entrada (<see cref="MontarPergunta"/>), para que fique no
/// histórico (FR-037).
/// </summary>
public sealed class ContextoExecucaoProvider : AIContextProvider
{
    public const string InicioContexto = "<<<CONTEXTO_EXECUCAO>>>";
    public const string FimContexto = "<<<FIM_CONTEXTO_EXECUCAO>>>";
    public const string InicioPergunta = "<<<PERGUNTA_USUARIO>>>";
    public const string FimPergunta = "<<<FIM_PERGUNTA_USUARIO>>>";

    private readonly ContextoExecucaoIa _contexto;

    public ContextoExecucaoProvider(ContextoExecucaoIa contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _contexto = contexto;
    }

    /// <summary>Texto do contexto, sem a pergunta.</summary>
    public static string Montar(ContextoExecucaoIa contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return TextoDelimitado.Montar(InicioContexto, FimContexto, contexto with { Pergunta = null });
    }

    public static string MontarPergunta(string pergunta)
    {
        ArgumentNullException.ThrowIfNull(pergunta);
        return TextoDelimitado.Montar(InicioPergunta, FimPergunta, pergunta);
    }

    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new AIContext { Messages = [new ChatMessage(ChatRole.User, Montar(_contexto))] });
}
