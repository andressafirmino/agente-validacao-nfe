using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Api.RAG.Configuracao;
using Api.RAG.Contratos;
using Microsoft.Extensions.Options;

namespace Api.RAG.Servicos;

public sealed partial class ValidadorIngestaoRag(IOptions<RagOpcoes> opcoes)
{
    private const string VersaoContrato = "1.0";

    public string? Validar(IngestaoDocumentoRagV1? pedido)
    {
        if (pedido is null)
        {
            return "JSON inválido.";
        }

        if (pedido.VersaoContrato != VersaoContrato)
        {
            return "versaoContrato deve ser 1.0.";
        }

        if (string.IsNullOrWhiteSpace(pedido.DocumentoIdExterno) || pedido.DocumentoIdExterno.Length > 128)
        {
            return "documentoIdExterno inválido.";
        }

        if (string.IsNullOrWhiteSpace(pedido.Titulo) || pedido.Titulo.Length > 512)
        {
            return "titulo inválido.";
        }

        if (string.IsNullOrWhiteSpace(pedido.VersaoDocumento) || pedido.VersaoDocumento.Length > 32)
        {
            return "versaoDocumento inválido.";
        }

        if (!DateOnly.TryParse(pedido.VigenciaInicio, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return "vigenciaInicio inválida.";
        }

        if (pedido.VigenciaFim is { Length: > 0 } fim && !DateOnly.TryParse(fim, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return "vigenciaFim inválida.";
        }

        if (string.IsNullOrWhiteSpace(pedido.ConteudoTexto))
        {
            return "conteudoTexto obrigatório.";
        }

        var bytes = Encoding.UTF8.GetByteCount(pedido.ConteudoTexto);
        if (bytes > opcoes.Value.LimiteConteudoBytes)
        {
            return "Conteúdo acima do limite configurado.";
        }

        if (ContemDocumentoPessoal(pedido.ConteudoTexto))
        {
            return "conteudoTexto contém padrão de documento pessoal não permitido.";
        }

        return null;
    }

    public static DateOnly ParseData(string iso) =>
        DateOnly.Parse(iso, CultureInfo.InvariantCulture);

    public static DateOnly? ParseDataOpcional(string? iso) =>
        string.IsNullOrWhiteSpace(iso) ? null : ParseData(iso);

    private static bool ContemDocumentoPessoal(string texto) =>
        CpfCompleto().IsMatch(texto) || CnpjCompleto().IsMatch(texto);

    [GeneratedRegex(@"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b")]
    private static partial Regex CpfCompleto();

    [GeneratedRegex(@"\b\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}\b")]
    private static partial Regex CnpjCompleto();
}
