using System.ComponentModel.DataAnnotations;

namespace Api.RAG.Configuracao;

/// <summary>PostgreSQL, ingestão e recuperação vetorial (spec 002).</summary>
public sealed class RagOpcoes
{
    public const string Secao = "Rag";

    public enum ModoRecuperacaoValor
    {
        Semente,
        Vetorial,
    }

    /// <summary>Obrigatória para rotas de ingestão; em Development pode vir do appsettings local.</summary>
    public string? ChaveAdministracao { get; set; }

    [Range(1024, 10_000_000)]
    public int LimiteConteudoBytes { get; set; } = 2_000_000;

    [Range(200, 8000)]
    public int ChunkTamanhoCaracteres { get; set; } = 2000;

    [Range(0, 2000)]
    public int ChunkSobreposicaoCaracteres { get; set; } = 200;

    [Range(128, 4096)]
    public int EmbeddingDimensao { get; set; } = 1536;

    public ModoRecuperacaoValor ModoRecuperacao { get; set; } = ModoRecuperacaoValor.Semente;

    public bool PermitirFallbackSemente { get; set; } = true;

    public bool AplicarMigracoes { get; set; }

    /// <summary>Modelo OpenAI de embedding (DP-15).</summary>
    [Required]
    public string ModeloEmbedding { get; set; } = "text-embedding-3-small";
}
