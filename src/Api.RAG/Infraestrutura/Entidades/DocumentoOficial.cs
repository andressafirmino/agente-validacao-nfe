namespace Api.RAG.Infraestrutura.Entidades;

public sealed class DocumentoOficial
{
    public Guid Id { get; set; }

    public required string DocumentoIdExterno { get; set; }

    public required string Titulo { get; set; }

    public required string TipoDocumento { get; set; }

    public required string Modelo { get; set; }

    public required string VersaoLayout { get; set; }

    public required string VersaoDocumento { get; set; }

    public DateOnly VigenciaInicio { get; set; }

    public DateOnly? VigenciaFim { get; set; }

    public required string HashConteudo { get; set; }

    public string? Fonte { get; set; }

    public DateTimeOffset IngestidoEm { get; set; }

    public required string Responsavel { get; set; }

    public ICollection<ChunkIndexado> Chunks { get; set; } = [];
}
