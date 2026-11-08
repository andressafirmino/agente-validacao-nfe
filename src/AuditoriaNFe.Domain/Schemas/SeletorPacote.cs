namespace AuditoriaNFe.Domain.Schemas;

/// <summary>
/// Escolhe o pacote vigente na data de referência e aplica a convivência genérica por
/// <see cref="VigenciaPacote.FimConvivencia"/> (data-model §5; FR-010b, FR-010c, FR-010f).
/// </summary>
public sealed class SeletorPacote
{
    public SeletorPacote(IReadOnlyList<VigenciaPacote> pacotes) =>
        throw new NotImplementedException("T062");

    /// <summary>Pacote com <c>Inicio ≤ data ≤ Fim</c>; o último vale até <paramref name="hoje"/>.</summary>
    public VigenciaPacote? Vigente(DateOnly dataReferencia, DateOnly hoje) =>
        throw new NotImplementedException("T062");

    /// <summary>
    /// Valida no pacote vigente e, se houver erro e a data estiver na convivência do anterior, também no
    /// anterior; sem pacote vigente, não chama <paramref name="validarNoPacote"/>.
    /// </summary>
    public ResultadoPacote<TErro> Validar<TErro>(DateOnly dataReferencia, DateOnly hoje, Func<VigenciaPacote, IReadOnlyList<TErro>> validarNoPacote) =>
        throw new NotImplementedException("T062");
}
