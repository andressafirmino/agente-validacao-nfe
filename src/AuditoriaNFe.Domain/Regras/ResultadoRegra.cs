namespace AuditoriaNFe.Domain.Regras;

/// <summary>Resultado de uma regra avaliada em um documento (data-model §3, <c>regrasAplicadas</c>).</summary>
public enum ResultadoRegra
{
    Conforme,
    Divergente,

    /// <summary>Condição da regra não atendida ou grupo ausente no documento.</summary>
    NaoAplicavel,
}
