using Api.RAG.Contratos;
using Api.RAG.Documentacao;
using Api.RAG.Servicos;

namespace Api.RAG.Endpoints;

public static class IngestaoPdfEndpoints
{
    public static void Mapear(WebApplication app)
    {
        app.MapPost("/api/v1/rag/ingestao/pdf", IngerirPdfAsync)
            .DisableAntiforgery()
            .WithName("IngerirPdfRag")
            .WithTags("Ingestao")
            .WithSummary("Ingerir PDF (multipart/form-data)")
            .AplicarSegurancaBearerAdminOpenApi();
    }

    private static async Task<IResult> IngerirPdfAsync(
        HttpContext contexto,
        IFormFile? arquivo,
        ServicoIngestao servico,
        ValidadorIngestaoRag validador,
        ExtraidorTextoPdf extrator,
        CancellationToken cancelamento)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return Results.BadRequest();
        }

        if (!string.Equals(arquivo.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            && !arquivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest();
        }

        await using var fluxo = arquivo.OpenReadStream();
        using var memoria = new MemoryStream();
        await fluxo.CopyToAsync(memoria, cancelamento).ConfigureAwait(false);
        var bytes = memoria.ToArray();
        var texto = extrator.Extrair(bytes);
        if (texto is null)
        {
            return Results.BadRequest();
        }

        var formulario = contexto.Request.Form;
        var pedido = new IngestaoDocumentoRagV1
        {
            VersaoContrato = "1.0",
            DocumentoIdExterno = formulario["documentoIdExterno"].ToString(),
            Titulo = formulario["titulo"].ToString(),
            TipoDocumento = string.IsNullOrWhiteSpace(formulario["tipoDocumento"]) ? "NF-e" : formulario["tipoDocumento"].ToString(),
            Modelo = string.IsNullOrWhiteSpace(formulario["modelo"]) ? "55" : formulario["modelo"].ToString(),
            VersaoLayout = string.IsNullOrWhiteSpace(formulario["versaoLayout"]) ? "4.00" : formulario["versaoLayout"].ToString(),
            VersaoDocumento = formulario["versaoDocumento"].ToString(),
            VigenciaInicio = formulario["vigenciaInicio"].ToString(),
            VigenciaFim = string.IsNullOrWhiteSpace(formulario["vigenciaFim"]) ? null : formulario["vigenciaFim"].ToString(),
            Fonte = string.IsNullOrWhiteSpace(formulario["fonte"]) ? arquivo.FileName : formulario["fonte"].ToString(),
            RegrasRelacionadas = ParseRegras(formulario["regrasRelacionadas"].ToString()),
            ConteudoTexto = texto,
        };

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
            var resultado = await servico.IngerirAsync(pedido, responsavel, cancelamento).ConfigureAwait(false);
            var corpo = new RespostaIngestaoRagV1(resultado.JobId, resultado.DocumentoId, resultado.ChunksIndexados);
            return resultado.Idempotente
                ? Results.Ok(corpo)
                : Results.Json(corpo, statusCode: StatusCodes.Status202Accepted);
        }
        catch (ServicoIngestao.FalhaEmbeddingException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static string[]? ParseRegras(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto))
        {
            return null;
        }

        return bruto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
