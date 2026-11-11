using Pgvector;

namespace Api.RAG.Infraestrutura.Entidades;

public sealed class ChunkIndexado
{
    public Guid Id { get; set; }

    public required string ChunkId { get; set; }

    public Guid DocumentoId { get; set; }

    public DocumentoOficial Documento { get; set; } = null!;

    public int Sequencia { get; set; }

    public required string Trecho { get; set; }

    public string? Secao { get; set; }

    public int? Pagina { get; set; }

    public string[] RegrasRelacionadas { get; set; } = [];

    public string[] PalavrasChave { get; set; } = [];

    public Vector? Embedding { get; set; }
}
