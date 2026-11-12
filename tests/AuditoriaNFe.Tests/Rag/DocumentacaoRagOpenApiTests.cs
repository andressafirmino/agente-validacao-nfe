using System.Net;
using System.Text.Json;
using Api.RAG.Documentacao;
using AuditoriaNFe.Tests.Integracao.Infra;

namespace AuditoriaNFe.Tests.Rag;

[Collection(ColecaoRagPostgres.Nome)]
public sealed class DocumentacaoRagOpenApiTests(RagPostgresIntegracaoFixture ambiente) : IAsyncLifetime
{
    private ServidorApiRagMcp? _servidor;

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _servidor = new ServidorApiRagMcp();
        await _servidor.IniciarAsync(ambiente.ConnectionString, cancellationToken: Cancelamento);
    }

    public async ValueTask DisposeAsync()
    {
        if (_servidor is not null)
        {
            await _servidor.DisposeAsync();
        }
    }

    [Fact]
    public async Task Ingestao_pdf_openapi_expoe_multipart_com_metadados_obrigatorios()
    {
        using var cliente = new HttpClient { BaseAddress = new Uri(_servidor!.BaseUrl) };
        using var resposta = await cliente.GetAsync(DocumentacaoRagExtensions.RotaOpenApi, Cancelamento);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        await using var fluxo = await resposta.Content.ReadAsStreamAsync(Cancelamento);
        using var documento = await JsonDocument.ParseAsync(fluxo, cancellationToken: Cancelamento);

        var schema = documento
            .RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/rag/ingestao/pdf")
            .GetProperty("post")
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("multipart/form-data")
            .GetProperty("schema");

        Assert.True(schema.TryGetProperty("properties", out var propriedades));
        foreach (var campo in new[] { "arquivo", "documentoIdExterno", "titulo", "versaoDocumento", "vigenciaInicio" })
        {
            Assert.True(propriedades.TryGetProperty(campo, out _), $"Campo OpenAPI ausente: {campo}");
        }

        var obrigatorios = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal);
        foreach (var campo in new[] { "arquivo", "documentoIdExterno", "titulo", "versaoDocumento", "vigenciaInicio" })
        {
            Assert.Contains(campo, obrigatorios);
        }
    }
}
