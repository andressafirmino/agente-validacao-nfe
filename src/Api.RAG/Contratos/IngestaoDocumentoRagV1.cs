using System.Text.Json.Serialization;

namespace Api.RAG.Contratos;

/// <summary>Corpo JSON do POST <c>/api/v1/rag/ingestao</c> (schema ingestao-rag-v1).</summary>
public sealed class IngestaoDocumentoRagV1
{
    [JsonPropertyName("versaoContrato")]
    public string VersaoContrato { get; set; } = "1.0";

    [JsonPropertyName("documentoIdExterno")]
    public string DocumentoIdExterno { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("tipoDocumento")]
    public string TipoDocumento { get; set; } = "NF-e";

    [JsonPropertyName("modelo")]
    public string Modelo { get; set; } = "55";

    [JsonPropertyName("versaoLayout")]
    public string VersaoLayout { get; set; } = "4.00";

    [JsonPropertyName("versaoDocumento")]
    public string VersaoDocumento { get; set; } = string.Empty;

    [JsonPropertyName("vigenciaInicio")]
    public string VigenciaInicio { get; set; } = string.Empty;

    [JsonPropertyName("vigenciaFim")]
    public string? VigenciaFim { get; set; }

    [JsonPropertyName("fonte")]
    public string? Fonte { get; set; }

    [JsonPropertyName("regrasRelacionadas")]
    public string[]? RegrasRelacionadas { get; set; }

    [JsonPropertyName("conteudoTexto")]
    public string ConteudoTexto { get; set; } = string.Empty;
}

public sealed record RespostaIngestaoRagV1(
    [property: JsonPropertyName("jobId")] Guid JobId,
    [property: JsonPropertyName("documentoId")] Guid DocumentoId,
    [property: JsonPropertyName("chunksIndexados")] int ChunksIndexados);
