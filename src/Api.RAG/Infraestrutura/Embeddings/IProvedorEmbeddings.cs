namespace Api.RAG.Infraestrutura.Embeddings;

/// <summary>Gera vetores para chunks (implementação OpenAI na Fase 3).</summary>
public interface IProvedorEmbeddings
{
    Task<ResultadoEmbedding> GerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default);
}
