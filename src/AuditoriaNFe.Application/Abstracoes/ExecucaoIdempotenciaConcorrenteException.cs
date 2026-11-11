namespace AuditoriaNFe.Application.Abstracoes;

/// <summary>Violação do índice único de <c>chaveIdempotenciaSofia</c> (execução concorrente, FR-043h).</summary>
public sealed class ExecucaoIdempotenciaConcorrenteException : Exception
{
    public ExecucaoIdempotenciaConcorrenteException()
    {
    }

    public ExecucaoIdempotenciaConcorrenteException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
