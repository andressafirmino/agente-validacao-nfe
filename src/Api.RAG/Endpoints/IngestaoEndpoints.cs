using System.Text.Json;
using Api.RAG.Contratos;
using Api.RAG.Documentacao;
using Api.RAG.Servicos;
namespace Api.RAG.Endpoints;

public static class IngestaoEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Mapear(WebApplication app)
    {
        app.MapPost("/api/v1/rag/ingestao", IngerirAsync)
            .WithName("IngerirDocumentoRag")
            .WithTags("Ingestao")
            .WithSummary("Ingerir pacote JSON (contrato ingestao-rag-v1)")
            .AplicarSegurancaBearerAdminOpenApi();

        if (DocumentacaoRagExtensions.Habilitada(app.Configuration))
        {
            app.MapGet("/api/v1/rag/ingestao", () =>
                    Results.Redirect($"{DocumentacaoRagExtensions.RotaSwaggerUi}/index.html"))
                .WithName("NavegarIngestaoRag")
                .WithTags("Ingestao")
                .WithSummary("Abre o Swagger UI (GET no navegador)")
                .ExcludeFromDescription();
        }
    }

    private static async Task<IResult> IngerirAsync(
        HttpContext contexto,
        ServicoIngestao servico,
        ValidadorIngestaoRag validador,
        CancellationToken cancelamento)
    {
        IngestaoDocumentoRagV1? pedido;
        try
        {
            pedido = await JsonSerializer.DeserializeAsync<IngestaoDocumentoRagV1>(contexto.Request.Body, Json, cancelamento)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        var erro = validador.Validar(pedido);
        if (erro is not null)
        {
            return erro.Contains("limite", StringComparison.OrdinalIgnoreCase)
                ? Results.StatusCode(StatusCodes.Status413PayloadTooLarge)
                : Results.BadRequest();
        }

        var responsavel = contexto.Request.Headers["X-Rag-Responsavel"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(responsavel))
        {
            responsavel = "admin-ingestao";
        }

        responsavel = responsavel.Length > 128 ? responsavel[..128] : responsavel;

        try
        {
            var resultado = await servico.IngerirAsync(pedido!, responsavel, cancelamento).ConfigureAwait(false);
            var corpo = new RespostaIngestaoRagV1(resultado.JobId, resultado.DocumentoId, resultado.ChunksIndexados);
            return resultado.Idempotente
                ? Results.Ok(corpo)
                : Results.Json(corpo, Json, statusCode: StatusCodes.Status202Accepted);
        }
        catch (ServicoIngestao.FalhaEmbeddingException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
