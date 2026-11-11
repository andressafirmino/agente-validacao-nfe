using Api.RAG.Infraestrutura;
using Api.RAG.Infraestrutura.Entidades;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AuditoriaNFe.Tests.Rag;

[Collection(ColecaoRagPostgres.Nome)]
public sealed class RagDbContextTests(RagPostgresIntegracaoFixture ambiente) : IAsyncLifetime
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private RagDbContext Contexto()
    {
        var builder = new DbContextOptionsBuilder<RagDbContext>();
        RagDbContext.Configurar(builder, ambiente.ConnectionString);
        return new RagDbContext(builder.Options);
    }

    public async ValueTask InitializeAsync()
    {
        await using var contexto = Contexto();
        await contexto.Database.MigrateAsync(Cancelamento);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migracao_com_usuario_da_aplicacao_cria_tabelas_no_schema_rag()
    {
        await using var conexao = new NpgsqlConnection(ambiente.ConnectionString);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new NpgsqlCommand(
            """
            SELECT table_schema || '.' || table_name
            FROM information_schema.tables
            WHERE table_schema = 'rag'
            ORDER BY 1
            """,
            conexao);
        var tabelas = new List<string>();
        await using (var leitor = await comando.ExecuteReaderAsync(Cancelamento))
        {
            while (await leitor.ReadAsync(Cancelamento))
            {
                tabelas.Add(leitor.GetString(0));
            }
        }

        Assert.Equal(
        [
            "rag.__EFMigrationsHistory",
            "rag.chamadas_embedding",
            "rag.chunks",
            "rag.documentos",
            "rag.jobs_ingestao",
        ],
            tabelas);
    }

    [Fact]
    public async Task Documento_e_chunk_persistem_com_arrays_e_embedding_opcional()
    {
        var documentoId = Guid.CreateVersion7();
        await using (var contexto = Contexto())
        {
            contexto.Documentos.Add(new DocumentoOficial
            {
                Id = documentoId,
                DocumentoIdExterno = "NT-TEST-001",
                Titulo = "Manual sintético",
                TipoDocumento = "NF-e",
                Modelo = "55",
                VersaoLayout = "4.00",
                VersaoDocumento = "v1",
                VigenciaInicio = new DateOnly(2026, 1, 1),
                HashConteudo = new string('a', 64),
                IngestidoEm = DateTimeOffset.UtcNow,
                Responsavel = "teste",
            });
            contexto.Chunks.Add(new ChunkIndexado
            {
                Id = Guid.CreateVersion7(),
                ChunkId = "chunk-teste-001",
                DocumentoId = documentoId,
                Sequencia = 1,
                Trecho = "Trecho fiscal de teste.",
                RegrasRelacionadas = ["W16-10"],
                PalavrasChave = ["icms"],
            });
            await contexto.SaveChangesAsync(Cancelamento);
        }

        await using var leitura = Contexto();
        var chunk = await leitura.Chunks.SingleAsync(c => c.ChunkId == "chunk-teste-001", Cancelamento);
        Assert.Equal(["W16-10"], chunk.RegrasRelacionadas);
        Assert.Null(chunk.Embedding);
    }

    [Fact]
    public async Task Chamada_embedding_recusa_tokens_negativos()
    {
        await using var contexto = Contexto();
        contexto.ChamadasEmbedding.Add(new ChamadaEmbedding
        {
            Id = Guid.CreateVersion7(),
            Modelo = "text-embedding-3-small",
            TokensEntrada = -1,
            RegistradoEm = DateTimeOffset.UtcNow,
        });

        var excecao = await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync(Cancelamento));
        Assert.Contains("ck_chamadas_embedding_tokens", excecao.InnerException!.Message, StringComparison.Ordinal);
    }
}
