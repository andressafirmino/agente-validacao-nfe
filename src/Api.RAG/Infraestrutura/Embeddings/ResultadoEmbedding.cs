namespace Api.RAG.Infraestrutura.Embeddings;

public sealed record ResultadoEmbedding(IReadOnlyList<float> Vetor, int TokensEntrada);
