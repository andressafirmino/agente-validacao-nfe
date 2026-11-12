using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.RAG.Contratos;
using Api.RAG.Infraestrutura;
using Api.RAG.Servicos;
using AuditoriaNFe.Infrastructure.Mcp;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AuditoriaNFe.Tests.Rag;

[Collection(ColecaoRagPostgres.Nome)]
public sealed class BuscaVetorialMcpIntegracaoTests(RagPostgresIntegracaoFixture ambiente) : IAsyncLifetime
{
    private const string ChaveAdmin = "chave-admin-teste-rag-mcp-vetorial";
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
    }

    public async ValueTask DisposeAsync()
    {
        if (_servidor is not null)
        {
            await _servidor.DisposeAsync();
        }
    }

    private async Task PrepararServidorIngeridoAsync(bool fallbackSemente)
    {
        await using (var contexto = CriarContexto())
        {
            contexto.ChamadasEmbedding.RemoveRange(contexto.ChamadasEmbedding);
            contexto.Chunks.RemoveRange(contexto.Chunks);
            contexto.JobsIngestao.RemoveRange(contexto.JobsIngestao);
            contexto.Documentos.RemoveRange(contexto.Documentos);
            await contexto.SaveChangesAsync(Cancelamento);
        }

        _servidor = new ServidorApiRagMcp();
        await _servidor.IniciarAsync(
            ambiente.ConnectionString,
            ChaveAdmin,
            ProvedorEmbeddingsDeterministico.Registrar,
            new Dictionary<string, string?>
            {
                ["Rag__ModoRecuperacao"] = "Vetorial",
                ["Rag__PermitirFallbackSemente"] = fallbackSemente ? "true" : "false",
            },
            Cancelamento);

        using var cliente = new HttpClient { BaseAddress = new Uri(_servidor.BaseUrl) };
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ChaveAdmin);
        var resposta = await cliente.PostAsync(
            "/api/v1/rag/ingestao",
            new StringContent(FixtureJson, Encoding.UTF8, "application/json"),
            Cancelamento);
        resposta.EnsureSuccessStatusCode();
    }

    private RagDbContext CriarContexto()
    {
        var builder = new DbContextOptionsBuilder<RagDbContext>();
        RagDbContext.Configurar(builder, ambiente.ConnectionString);
        return new RagDbContext(builder.Options);
    }

    private async Task<RespostaBusca> BuscarViaMcpAsync(string dataReferencia, string consulta, string[] regras)
    {
        var transporte = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(_servidor!.EndpointMcp),
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = TimeSpan.FromSeconds(30),
            },
            NullLoggerFactory.Instance);

        await using var cliente = await McpClient.CreateAsync(transporte, loggerFactory: NullLoggerFactory.Instance, cancellationToken: Cancelamento);
        var argumentos = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tipoDocumento"] = "NF-e",
            ["modelo"] = "55",
            ["versaoLayout"] = "4.00",
            ["dataReferencia"] = dataReferencia,
            ["consulta"] = consulta,
            ["regrasRelacionadas"] = regras,
            ["quantidadeMaxima"] = 5,
        };

        var resultado = await cliente.CallToolAsync(ConsultaManuaisMcp.NomeFerramenta, argumentos, cancellationToken: Cancelamento);
        Assert.NotEqual(true, resultado.IsError);
        var json = resultado.StructuredContent is not null
            ? JsonSerializer.Serialize(resultado.StructuredContent)
            : resultado.Content.OfType<TextContentBlock>().Select(t => t.Text).First();
        return JsonSerializer.Deserialize<RespostaBusca>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Resposta MCP inválida.");
    }

    [Fact]
    public async Task Apos_ingestao_MCP_retorna_chunk_da_base_vetorial()
    {
        await PrepararServidorIngeridoAsync(fallbackSemente: false);
        var resposta = await BuscarViaMcpAsync("2026-06-01", "Grupo tributação W16 CST CFOP layout 4.00", ["W16-10"]);

        Assert.Equal("0.1", resposta.VersaoContrato);
        Assert.NotEmpty(resposta.Trechos);
        Assert.All(resposta.Trechos, t => Assert.StartsWith("nt-sintetica-2026-001-", t.ChunkId, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(resposta.Trechos, t => t.ChunkId == "nt-sintetico-w04-10-001");
    }

    [Fact]
    public async Task Data_referencia_fora_da_vigencia_retorna_lista_vazia_sem_fallback()
    {
        await PrepararServidorIngeridoAsync(fallbackSemente: false);
        var resposta = await BuscarViaMcpAsync("2019-01-01", "W16 CST CFOP", ["W16-10"]);
        Assert.Empty(resposta.Trechos);
    }
}
