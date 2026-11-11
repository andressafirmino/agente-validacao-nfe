namespace Api.RAG.Infraestrutura.Entidades;

public sealed class ChamadaEmbedding
{
    public Guid Id { get; set; }

    public Guid? JobIngestaoId { get; set; }

    public JobIngestao? JobIngestao { get; set; }

    public required string Modelo { get; set; }

    public int TokensEntrada { get; set; }

    public decimal? CustoEstimadoUsd { get; set; }

    public DateTimeOffset RegistradoEm { get; set; }
}
