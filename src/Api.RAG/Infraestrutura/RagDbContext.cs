using Api.RAG.Infraestrutura.Entidades;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace Api.RAG.Infraestrutura;

/// <summary>Base de conhecimento no schema <c>rag</c> (feature 002).</summary>
public sealed class RagDbContext(DbContextOptions<RagDbContext> options) : DbContext(options)
{
    public const string Schema = "rag";
    public const string NomeConnectionString = "Rag";

    public DbSet<DocumentoOficial> Documentos => Set<DocumentoOficial>();
    public DbSet<ChunkIndexado> Chunks => Set<ChunkIndexado>();
    public DbSet<JobIngestao> JobsIngestao => Set<JobIngestao>();
    public DbSet<ChamadaEmbedding> ChamadasEmbedding => Set<ChamadaEmbedding>();

    public static void Configurar(DbContextOptionsBuilder builder, string connectionString)
    {
        const int timeoutSegundos = 5;
        var conexao = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = timeoutSegundos,
            CommandTimeout = timeoutSegundos,
        };

        builder.UseNpgsql(conexao.ConnectionString, npgsql => npgsql
            .CommandTimeout(timeoutSegundos)
            .UseVector()
            .MigrationsHistoryTable("__EFMigrationsHistory", Schema));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        var dimensao = 1536;

        modelBuilder.Entity<DocumentoOficial>(documento =>
        {
            documento.ToTable("documentos");
            documento.HasKey(d => d.Id).HasName("pk_documentos");
            documento.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
            documento.Property(d => d.DocumentoIdExterno).HasColumnName("documento_id_externo").HasMaxLength(128).IsRequired();
            documento.Property(d => d.Titulo).HasColumnName("titulo").HasMaxLength(512).IsRequired();
            documento.Property(d => d.TipoDocumento).HasColumnName("tipo_documento").HasMaxLength(16).IsRequired();
            documento.Property(d => d.Modelo).HasColumnName("modelo").HasMaxLength(8).IsRequired();
            documento.Property(d => d.VersaoLayout).HasColumnName("versao_layout").HasMaxLength(16).IsRequired();
            documento.Property(d => d.VersaoDocumento).HasColumnName("versao_documento").HasMaxLength(32).IsRequired();
            documento.Property(d => d.VigenciaInicio).HasColumnName("vigencia_inicio");
            documento.Property(d => d.VigenciaFim).HasColumnName("vigencia_fim");
            documento.Property(d => d.HashConteudo).HasColumnName("hash_conteudo").HasMaxLength(64).IsRequired();
            documento.Property(d => d.Fonte).HasColumnName("fonte").HasMaxLength(256);
            documento.Property(d => d.IngestidoEm).HasColumnName("ingestido_em");
            documento.Property(d => d.Responsavel).HasColumnName("responsavel").HasMaxLength(128).IsRequired();

            documento.HasIndex(d => new { d.DocumentoIdExterno, d.VersaoDocumento, d.HashConteudo })
                .IsUnique()
                .HasDatabaseName("ux_documentos_externo_versao_hash");
            documento.HasMany(d => d.Chunks).WithOne(c => c.Documento).HasForeignKey(c => c.DocumentoId)
                .HasConstraintName("fk_chunks_documentos");
        });

        modelBuilder.Entity<ChunkIndexado>(chunk =>
        {
            chunk.ToTable("chunks");
            chunk.HasKey(c => c.Id).HasName("pk_chunks");
            chunk.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            chunk.Property(c => c.ChunkId).HasColumnName("chunk_id").HasMaxLength(128).IsRequired();
            chunk.Property(c => c.DocumentoId).HasColumnName("documento_id");
            chunk.Property(c => c.Sequencia).HasColumnName("sequencia");
            chunk.Property(c => c.Trecho).HasColumnName("trecho").IsRequired();
            chunk.Property(c => c.Secao).HasColumnName("secao").HasMaxLength(128);
            chunk.Property(c => c.Pagina).HasColumnName("pagina");
            chunk.Property(c => c.RegrasRelacionadas).HasColumnName("regras_relacionadas");
            chunk.Property(c => c.PalavrasChave).HasColumnName("palavras_chave");
            chunk.Property(c => c.Embedding).HasColumnName("embedding").HasColumnType($"vector({dimensao})");

            chunk.HasIndex(c => c.ChunkId).IsUnique().HasDatabaseName("ux_chunks_chunk_id");
            chunk.HasIndex(c => c.DocumentoId).HasDatabaseName("ix_chunks_documento_id");
        });

        modelBuilder.Entity<JobIngestao>(job =>
        {
            job.ToTable("jobs_ingestao", tabela =>
            {
                tabela.HasCheckConstraint(
                    "ck_jobs_ingestao_estado",
                    "estado IN ('Pendente', 'Processando', 'Concluido', 'Falhou')");
            });
            job.HasKey(j => j.Id).HasName("pk_jobs_ingestao");
            job.Property(j => j.Id).HasColumnName("id").ValueGeneratedNever();
            job.Property(j => j.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(16).IsRequired();
            job.Property(j => j.DocumentoIdExterno).HasColumnName("documento_id_externo").HasMaxLength(128).IsRequired();
            job.Property(j => j.VersaoDocumento).HasColumnName("versao_documento").HasMaxLength(32).IsRequired();
            job.Property(j => j.HashConteudo).HasColumnName("hash_conteudo").HasMaxLength(64).IsRequired();
            job.Property(j => j.MensagemErro).HasColumnName("mensagem_erro");
            job.Property(j => j.IniciadoEm).HasColumnName("iniciado_em");
            job.Property(j => j.ConcluidoEm).HasColumnName("concluido_em");
        });

        modelBuilder.Entity<ChamadaEmbedding>(chamada =>
        {
            chamada.ToTable("chamadas_embedding", tabela =>
            {
                tabela.HasCheckConstraint("ck_chamadas_embedding_tokens", "tokens_entrada >= 0");
            });
            chamada.HasKey(c => c.Id).HasName("pk_chamadas_embedding");
            chamada.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            chamada.Property(c => c.JobIngestaoId).HasColumnName("job_ingestao_id");
            chamada.Property(c => c.Modelo).HasColumnName("modelo").HasMaxLength(64).IsRequired();
            chamada.Property(c => c.TokensEntrada).HasColumnName("tokens_entrada");
            chamada.Property(c => c.CustoEstimadoUsd).HasColumnName("custo_estimado_usd").HasPrecision(12, 6);
            chamada.Property(c => c.RegistradoEm).HasColumnName("registrado_em");

            chamada.HasOne(c => c.JobIngestao).WithMany(j => j.ChamadasEmbedding).HasForeignKey(c => c.JobIngestaoId)
                .HasConstraintName("fk_chamadas_embedding_jobs");
        });
    }
}
