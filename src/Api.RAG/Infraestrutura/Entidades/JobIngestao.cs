namespace Api.RAG.Infraestrutura.Entidades;

public sealed class JobIngestao
{
    public Guid Id { get; set; }

    public EstadoJobIngestao Estado { get; set; }

    public required string DocumentoIdExterno { get; set; }

    public required string VersaoDocumento { get; set; }

    public required string HashConteudo { get; set; }

    public string? MensagemErro { get; set; }

    public DateTimeOffset IniciadoEm { get; set; }

    public DateTimeOffset? ConcluidoEm { get; set; }

    public ICollection<ChamadaEmbedding> ChamadasEmbedding { get; set; } = [];
}
