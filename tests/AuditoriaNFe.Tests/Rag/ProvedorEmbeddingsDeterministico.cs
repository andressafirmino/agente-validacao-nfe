using System.Security.Cryptography;
using System.Text;
using Api.RAG.Infraestrutura.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuditoriaNFe.Tests.Rag;

/// <summary>Vetores determinísticos para testes de ingestão sem chamar a OpenAI.</summary>
public sealed class ProvedorEmbeddingsDeterministico : IProvedorEmbeddings
{
    private const int Dimensao = 1536;

    public Task<ResultadoEmbedding> GerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
        var vetor = new float[Dimensao];
        for (var i = 0; i < Dimensao; i++)
        {
            vetor[i] = hash[i % hash.Length] / 255f;
        }

        return Task.FromResult(new ResultadoEmbedding(vetor, Math.Max(1, texto.Length / 4)));
    }

    public static void Registrar(IServiceCollection servicos)
    {
        servicos.RemoveAll<IProvedorEmbeddings>();
        servicos.AddSingleton<IProvedorEmbeddings, ProvedorEmbeddingsDeterministico>();
    }
}
