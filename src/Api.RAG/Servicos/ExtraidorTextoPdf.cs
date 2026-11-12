using System.Text;
using UglyToad.PdfPig;

namespace Api.RAG.Servicos;

/// <summary>Extrai texto de PDF para ingestão (sem OCR).</summary>
public sealed class ExtraidorTextoPdf
{
    public string? Extrair(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var documento = PdfDocument.Open(bytes);
            var texto = new StringBuilder();
            foreach (var pagina in documento.GetPages())
            {
                var conteudo = pagina.Text;
                if (string.IsNullOrWhiteSpace(conteudo))
                {
                    continue;
                }

                if (texto.Length > 0)
                {
                    texto.AppendLine();
                    texto.AppendLine();
                }

                texto.Append(conteudo.Trim());
            }

            var resultado = texto.ToString().Trim();
            return resultado.Length == 0 ? null : resultado;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
