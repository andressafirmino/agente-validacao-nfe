using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.RAG.Contratos;
using Api.RAG.Infraestrutura;
using Api.RAG.Infraestrutura.Entidades;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.EntityFrameworkCore;

namespace AuditoriaNFe.Tests.Rag;

[Collection(ColecaoRagPostgres.Nome)]
public sealed class IngestaoRagIntegracaoTests(RagPostgresIntegracaoFixture ambiente) : IAsyncLifetime
{
  private const string ChaveAdmin = "chave-admin-teste-rag-ingestao-v1";
    private ServidorApiRagMcp? _servidor;

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private static string FixtureJson =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rag", "documento-sintetico.v1.json"));

    public async ValueTask InitializeAsync()
    {
        await using (var contexto = CriarContexto())
        {
            await contexto.Database.MigrateAsync(Cancelamento);
        }

        _servidor = new ServidorApiRagMcp();
        await _servidor.IniciarAsync(
            ambiente.ConnectionString,
            ChaveAdmin,
            ProvedorEmbeddingsDeterministico.Registrar,
            Cancelamento);
    }

    public async ValueTask DisposeAsync()
    {
        if (_servidor is not null)
        {
            await _servidor.DisposeAsync();
        }
    }

    private RagDbContext CriarContexto()
    {
        var builder = new DbContextOptionsBuilder<RagDbContext>();
        RagDbContext.Configurar(builder, ambiente.ConnectionString);
        return new RagDbContext(builder.Options);
    }

  private HttpClient CriarClienteAutenticado()
    {
        var cliente = new HttpClient { BaseAddress = new Uri(_servidor!.BaseUrl) };
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ChaveAdmin);
        return cliente;
    }

    [Fact]
    public async Task Post_sem_Bearer_retorna_401()
    {
        using var cliente = new HttpClient { BaseAddress = new Uri(_servidor!.BaseUrl) };
        using var conteudo = new StringContent(FixtureJson, Encoding.UTF8, "application/json");
        var resposta = await cliente.PostAsync("/api/v1/rag/ingestao", conteudo, Cancelamento);
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Ingestao_duas_vezes_e_idempotente_e_vigencia_gravada()
    {
        using var cliente = CriarClienteAutenticado();
        var primeira = await cliente.PostAsync(
            "/api/v1/rag/ingestao",
            new StringContent(FixtureJson, Encoding.UTF8, "application/json"),
            Cancelamento);
        Assert.Equal(HttpStatusCode.Accepted, primeira.StatusCode);
        var corpo1 = await primeira.Content.ReadFromJsonAsync<RespostaIngestaoRagV1>(Cancelamento);
        Assert.NotNull(corpo1);
        Assert.True(corpo1.ChunksIndexados > 0);

        var segunda = await cliente.PostAsync(
            "/api/v1/rag/ingestao",
            new StringContent(FixtureJson, Encoding.UTF8, "application/json"),
            Cancelamento);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        var corpo2 = await segunda.Content.ReadFromJsonAsync<RespostaIngestaoRagV1>(Cancelamento);
        Assert.NotNull(corpo2);
        Assert.Equal(corpo1.DocumentoId, corpo2.DocumentoId);
        Assert.Equal(corpo1.ChunksIndexados, corpo2.ChunksIndexados);

        await using var contexto = CriarContexto();
        var documento = await contexto.Documentos.SingleAsync(d => d.Id == corpo1.DocumentoId, Cancelamento);
        Assert.Equal(new DateOnly(2026, 1, 1), documento.VigenciaInicio);
        Assert.Null(documento.VigenciaFim);
        Assert.Equal(corpo1.ChunksIndexados, await contexto.Chunks.CountAsync(c => c.DocumentoId == documento.Id, Cancelamento));
    }
}
