using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AuditoriaNFe.Application.ContextProviders;

/// <summary>
/// Resumo da base da sessão (FR-037, FR-042): documentos e resultados determinísticos das execuções
/// anteriores, já minimizados (ADR-0004). Vem do armazenamento a cada execução, e não do histórico,
/// para que a redução do histórico não tire do agente os resultados das análises.
/// </summary>
public sealed record ContextoSessaoIa(
    int ExecucoesAnteriores,
    IReadOnlyList<DocumentoContextoIa> DocumentosAnteriores,
    IReadOnlyList<AchadoContextoIa> AchadosAnteriores,
    IReadOnlyList<RegraContextoIa> RegrasAnteriores,
    bool SemBaseAnalisada)
{
    public const string ObservacaoSemBase = "Não há documentos analisados nesta sessão.";

    public string? Observacao => SemBaseAnalisada ? ObservacaoSemBase : null;

    public static ContextoSessaoIa Nova(bool semBaseAnalisada) => new(0, [], [], [], semBaseAnalisada);
}

/// <summary>Entrega o <see cref="ContextoSessaoIa"/> como dado delimitado e não confiável, a cada chamada.</summary>
public sealed class ContextoSessaoProvider : AIContextProvider
{
    public const string InicioContexto = "<<<CONTEXTO_SESSAO>>>";
    public const string FimContexto = "<<<FIM_CONTEXTO_SESSAO>>>";

    private readonly ContextoSessaoIa _contexto;

    public ContextoSessaoProvider(ContextoSessaoIa contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _contexto = contexto;
    }

    public static string Montar(ContextoSessaoIa contexto) => TextoDelimitado.Montar(InicioContexto, FimContexto, contexto);

    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new AIContext { Messages = [new ChatMessage(ChatRole.User, Montar(_contexto))] });
}
