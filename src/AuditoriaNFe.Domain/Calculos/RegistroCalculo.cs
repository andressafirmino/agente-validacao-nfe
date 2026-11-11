namespace AuditoriaNFe.Domain.Calculos;

/// <summary>Registro persistido na execução (data-model §9).</summary>
public sealed class RegistroCalculo
{
    public RegistroCalculo(
        Guid id,
        OperacaoCalculo operacao,
        string campo,
        IReadOnlyList<DocumentoIncluidoCalculo> documentosIncluidos,
        IReadOnlyList<DocumentoExcluidoCalculo> documentosExcluidos,
        decimal resultado,
        DateTimeOffset registradoEm)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id do cálculo é obrigatório.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(campo);
        ArgumentNullException.ThrowIfNull(documentosIncluidos);
        ArgumentNullException.ThrowIfNull(documentosExcluidos);

        Id = id;
        Operacao = operacao;
        Campo = campo;
        DocumentosIncluidos = documentosIncluidos;
        DocumentosExcluidos = documentosExcluidos;
        Resultado = resultado;
        RegistradoEm = registradoEm;
    }

    public Guid Id { get; }
    public OperacaoCalculo Operacao { get; }
    public string Campo { get; }
    public IReadOnlyList<DocumentoIncluidoCalculo> DocumentosIncluidos { get; }
    public IReadOnlyList<DocumentoExcluidoCalculo> DocumentosExcluidos { get; }
    public decimal Resultado { get; }
    public DateTimeOffset RegistradoEm { get; }
}
