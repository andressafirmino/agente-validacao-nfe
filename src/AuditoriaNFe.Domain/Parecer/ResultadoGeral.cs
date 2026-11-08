namespace AuditoriaNFe.Domain.Parecer;

/// <summary>Resultado geral do parecer (FR-018), calculado pelo sistema e nunca pela IA.</summary>
public enum ResultadoGeral
{
    SemInconsistencias,
    ComInconsistencias,
    Inconclusiva,
}
