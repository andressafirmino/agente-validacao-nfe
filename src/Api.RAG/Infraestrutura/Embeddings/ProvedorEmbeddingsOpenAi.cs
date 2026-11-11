using System.ClientModel;
using Api.RAG.Configuracao;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;

namespace Api.RAG.Infraestrutura.Embeddings;

public sealed class ProvedorEmbeddingsOpenAi(IConfiguration configuracao, IOptions<RagOpcoes> opcoes) : IProvedorEmbeddings
{
    public async Task<ResultadoEmbedding> GerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default)
    {
        var apiKey = configuracao["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI:ApiKey é obrigatória para ingestão com embeddings.");
        }

        var modelo = opcoes.Value.ModeloEmbedding;
        var cliente = new OpenAIClient(new ApiKeyCredential(apiKey));
        var embeddingClient = cliente.GetEmbeddingClient(modelo);
        var resposta = await embeddingClient.GenerateEmbeddingAsync(texto, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var vetor = resposta.Value.ToFloats().ToArray();
        if (vetor.Length != opcoes.Value.EmbeddingDimensao)
        {
            throw new InvalidOperationException(
                $"Dimensão do embedding ({vetor.Length}) difere de Rag:EmbeddingDimensao ({opcoes.Value.EmbeddingDimensao}).");
        }

        var tokens = Math.Max(1, texto.Length / 4);
        return new ResultadoEmbedding(vetor, tokens);
    }
}
