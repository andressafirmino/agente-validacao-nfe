using AuditoriaNFe.Domain.Sessoes;

namespace AuditoriaNFe.Application.Ferramentas;

/// <summary>
/// Contexto da execução corrente para ferramentas de leitura (research R-06), propagado por
/// <see cref="AsyncLocal{T}"/> durante a etapa 7.
/// </summary>
public sealed class EscopoFerramentasExecucao : IDisposable
{
    private static readonly AsyncLocal<EscopoFerramentasExecucao?> EscopoAtual = new();

    private readonly EscopoFerramentasExecucao? _anterior;
    private bool _encerrado;

    private EscopoFerramentasExecucao(Execucao execucao, FerramentasEstadoExecucao estado)
    {
        Execucao = execucao ?? throw new ArgumentNullException(nameof(execucao));
        Estado = estado ?? throw new ArgumentNullException(nameof(estado));
        _anterior = EscopoAtual.Value;
    }

    public static EscopoFerramentasExecucao? Atual => EscopoAtual.Value;

    public Execucao Execucao { get; }
    public FerramentasEstadoExecucao Estado { get; }

    public static EscopoFerramentasExecucao Iniciar(Execucao execucao, FerramentasEstadoExecucao estado)
    {
        var escopo = new EscopoFerramentasExecucao(execucao, estado);
        EscopoAtual.Value = escopo;
        return escopo;
    }

    public void Dispose()
    {
        if (_encerrado)
        {
            return;
        }

        _encerrado = true;
        if (ReferenceEquals(EscopoAtual.Value, this))
        {
            EscopoAtual.Value = _anterior;
        }
    }
}
