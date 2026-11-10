namespace AuditoriaNFe.Domain.Schemas;

/// <summary>
/// Escolhe o pacote vigente na data de referência e aplica a convivência genérica por
/// <see cref="VigenciaPacote.FimConvivencia"/> (data-model §5; FR-010b, FR-010c, FR-010f).
/// </summary>
public sealed class SeletorPacote
{
    private readonly IReadOnlyList<VigenciaPacote> _pacotes;

    public SeletorPacote(IReadOnlyList<VigenciaPacote> pacotes)
    {
        ArgumentNullException.ThrowIfNull(pacotes);
        _pacotes = [.. pacotes.OrderBy(p => p.Ordem)];
    }

    /// <summary>Pacote com <c>Inicio ≤ data ≤ Fim</c>; o último vale até <paramref name="hoje"/>.</summary>
    public VigenciaPacote? Vigente(DateOnly dataReferencia, DateOnly hoje) =>
        _pacotes.FirstOrDefault(p => p.Inicio <= dataReferencia && dataReferencia <= (p.Fim ?? hoje));

    /// <summary>
    /// Valida no pacote vigente e, se houver erro e a data estiver na convivência do anterior, também no
    /// anterior; sem pacote vigente, não chama <paramref name="validarNoPacote"/>.
    /// </summary>
    public ResultadoPacote<TErro> Validar<TErro>(DateOnly dataReferencia, DateOnly hoje, Func<VigenciaPacote, IReadOnlyList<TErro>> validarNoPacote)
    {
        ArgumentNullException.ThrowIfNull(validarNoPacote);

        var vigente = Vigente(dataReferencia, hoje);
        if (vigente is null)
        {
            return new ResultadoPacote<TErro> { DataReferencia = dataReferencia };
        }

        var anterior = _pacotes.LastOrDefault(p => p.Ordem < vigente.Ordem);
        var emConvivencia = anterior?.FimConvivencia is { } fimConvivencia && dataReferencia <= fimConvivencia;

        var errosVigente = validarNoPacote(vigente);
        if (errosVigente.Count == 0 || !emConvivencia)
        {
            return new ResultadoPacote<TErro>
            {
                DataReferencia = dataReferencia,
                PacoteVigente = vigente,
                PacoteAplicado = vigente,
                PacotesTentados = [vigente.Id],
                EmConvivencia = emConvivencia,
                Erros = errosVigente,
            };
        }

        var errosAnterior = validarNoPacote(anterior!);
        return new ResultadoPacote<TErro>
        {
            DataReferencia = dataReferencia,
            PacoteVigente = vigente,
            PacoteAplicado = errosAnterior.Count == 0 ? anterior : vigente,
            PacotesTentados = [vigente.Id, anterior!.Id],
            EmConvivencia = true,
            Erros = errosAnterior.Count == 0 ? [] : errosVigente,
        };
    }
}
