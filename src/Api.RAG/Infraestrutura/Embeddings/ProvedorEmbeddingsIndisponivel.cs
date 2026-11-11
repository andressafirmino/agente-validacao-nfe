namespace Api.RAG.Infraestrutura.Embeddings;

/// <summary>Placeholder até T212; falha se ingestão chamar embeddings sem provedor real.</summary>
public sealed class ProvedorEmbeddingsIndisponivel : IProvedorEmbeddings
{
    public Task<ResultadoEmbedding> GerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Provedor de embeddings ainda não configurado.");
}
