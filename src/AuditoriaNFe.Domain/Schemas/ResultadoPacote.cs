namespace AuditoriaNFe.Domain.Schemas;

/// <summary>
/// Pacote escolhido para a validação de layout de um documento (data-model §3, <c>ValidacaoLayout</c>).
/// Sem pacote vigente na data, <see cref="Encontrado"/> é falso (ERR-023).
/// </summary>
public sealed record ResultadoPacote<TErro>
{
    public required DateOnly DataReferencia { get; init; }
    public VigenciaPacote? PacoteVigente { get; init; }

    /// <summary>Igual ao vigente, salvo convivência sem erros no pacote anterior (FR-010f).</summary>
    public VigenciaPacote? PacoteAplicado { get; init; }

    public IReadOnlyList<string> PacotesTentados { get; init; } = [];

    /// <summary>A data está na convivência do pacote anterior, tenha ele sido tentado ou não.</summary>
    public bool EmConvivencia { get; init; }

    /// <summary>Erros estruturais do pacote aplicado; na convivência com erros nos dois, os do vigente.</summary>
    public IReadOnlyList<TErro> Erros { get; init; } = [];

    public bool Encontrado => PacoteVigente is not null;
}
