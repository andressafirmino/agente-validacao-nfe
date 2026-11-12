using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.RAG.Contratos;
using Api.RAG.Infraestrutura;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.EntityFrameworkCore;

namespace AuditoriaNFe.Tests.Rag;

[Collection(ColecaoRagPostgres.Nome)]
public sealed class IngestaoPdfIntegracaoTests(RagPostgresIntegracaoFixture ambiente) : IAsyncLifetime
{
    private const string ChaveAdmin = "chave-admin-teste-rag-ingestao-pdf-v1";
    private ServidorApiRagMcp? _servidor;

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

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
            cancellationToken: Cancelamento);
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

    [Fact]
    public async Task Post_pdf_autenticado_indexa_texto()
    {
        using var cliente = new HttpClient { BaseAddress = new Uri(_servidor!.BaseUrl) };
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ChaveAdmin);

        using var formulario = new MultipartFormDataContent();
        var pdf = PdfMinimoComTexto.Gerar("Regra W04-10 ICMSTot vICMS soma itens");
        formulario.Add(new ByteArrayContent(pdf), "arquivo", "manual-teste.pdf");
        formulario.Add(new StringContent("PDF-TESTE-W04"), "documentoIdExterno");
        formulario.Add(new StringContent("Manual PDF teste"), "titulo");
        formulario.Add(new StringContent("v1"), "versaoDocumento");
        formulario.Add(new StringContent("2020-01-01"), "vigenciaInicio");
        formulario.Add(new StringContent("W04-10"), "regrasRelacionadas");

        var resposta = await cliente.PostAsync("/api/v1/rag/ingestao/pdf", formulario, Cancelamento);
        Assert.Equal(HttpStatusCode.Accepted, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<RespostaIngestaoRagV1>(Cancelamento);
        Assert.NotNull(corpo);
        Assert.True(corpo!.ChunksIndexados > 0);
    }
}
