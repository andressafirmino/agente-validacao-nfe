namespace AuditoriaNFe.Domain.Regras;

/// <summary>Resultado devolvido por uma <see cref="IImplementacaoRegra"/>.</summary>
public sealed record AvaliacaoRegra(ResultadoRegra Resultado, IReadOnlyList<DivergenciaRegra> Divergencias, string? Motivo)
{
    public static AvaliacaoRegra Conforme() => new(ResultadoRegra.Conforme, [], null);

    public static AvaliacaoRegra Divergente(params DivergenciaRegra[] divergencias) =>
        new(ResultadoRegra.Divergente, divergencias, null);

    public static AvaliacaoRegra NaoAplicavel(string motivo) => new(ResultadoRegra.NaoAplicavel, [], motivo);
}

/// <summary>
/// Divergência encontrada: campo no padrão de caminhos de <see cref="DadosDocumento"/>, valor
/// informado e valor calculado em texto invariante (FR-014).
/// </summary>
public sealed record DivergenciaRegra(string Campo, string ValorInformado, string ValorEsperado, int? Item = null);
