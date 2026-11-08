namespace AuditoriaNFe.Domain.Regras;

/// <summary>Registro de uma regra avaliada no documento, para a rastreabilidade (NFR-008, FR-013a).</summary>
public sealed record RegraAplicada(string RegraId, int VersaoRegra, ResultadoRegra Resultado, string? Motivo = null);
