using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Domain.Regras;

/// <summary>
/// Uma versão de regra do catálogo aprovado (data-model §6). Versões de uma mesma regra não se
/// sobrepõem; <see cref="VigenciaFim"/> nulo é vigência aberta.
/// </summary>
public sealed record RegraConteudo
{
    public required string Id { get; init; }
    public required int VersaoRegra { get; init; }
    public required GrupoRegra Grupo { get; init; }
    public required string Descricao { get; init; }
    public required FonteRegra Fonte { get; init; }
    public required DateOnly VigenciaInicio { get; init; }
    public DateOnly? VigenciaFim { get; init; }
    public required Severidade Severidade { get; init; }

    /// <summary>Diferença aceita para mais ou para menos; nula é comparação exata.</summary>
    public decimal? Tolerancia { get; init; }

    public required AplicacaoSefaz AplicacaoSefaz { get; init; }
    public IReadOnlyList<string> Condicoes { get; init; } = [];

    /// <summary>Id da <see cref="IImplementacaoRegra"/> que avalia esta versão.</summary>
    public required string Implementacao { get; init; }

    public bool VigenteEm(DateOnly dataReferencia) =>
        VigenciaInicio <= dataReferencia && (VigenciaFim is null || dataReferencia <= VigenciaFim);
}
