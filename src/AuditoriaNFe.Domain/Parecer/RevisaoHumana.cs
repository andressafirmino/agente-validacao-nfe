namespace AuditoriaNFe.Domain.Parecer;

/// <summary>Indicação de revisão humana do parecer e seus motivos (FR-024, FR-047b).</summary>
public sealed record RevisaoHumana(bool Necessaria, IReadOnlyList<string> Motivos)
{
    public const string MotivoResultadoInconclusivo = "resultado inconclusivo";
    public const string MotivoIndicioIa = "indício da IA de severidade Alta ou Média";
    public const string MotivoAusenciaEvidencias = "ausência de evidências para um ponto avaliado";
    public const string MotivoConsultaManuaisNaoHabilitada = "consulta a manuais oficiais não habilitada";
}
