using Api.RAG.Configuracao;
using Microsoft.Extensions.Options;

namespace Api.RAG.Servicos;

/// <summary>Segmentação por caracteres com preferência por parágrafos (research R-04).</summary>
public sealed class SegmentadorTexto(IOptions<RagOpcoes> opcoes)
{
    public IReadOnlyList<string> Segmentar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return [];
        }

        var tamanho = opcoes.Value.ChunkTamanhoCaracteres;
        var sobreposicao = Math.Min(opcoes.Value.ChunkSobreposicaoCaracteres, tamanho / 2);
        var paragrafos = texto.Split("\n\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var trechos = new List<string>();
        var buffer = string.Empty;

        foreach (var paragrafo in paragrafos)
        {
            if (buffer.Length == 0)
            {
                buffer = paragrafo;
            }
            else if (buffer.Length + 2 + paragrafo.Length <= tamanho)
            {
                buffer = $"{buffer}\n\n{paragrafo}";
            }
            else
            {
                trechos.AddRange(DividirComSobreposicao(buffer, tamanho, sobreposicao));
                buffer = paragrafo;
            }
        }

        if (buffer.Length > 0)
        {
            trechos.AddRange(DividirComSobreposicao(buffer, tamanho, sobreposicao));
        }

        return trechos;
    }

    private static IEnumerable<string> DividirComSobreposicao(string texto, int tamanho, int sobreposicao)
    {
        if (texto.Length <= tamanho)
        {
            yield return texto;
            yield break;
        }

        var inicio = 0;
        while (inicio < texto.Length)
        {
            var comprimento = Math.Min(tamanho, texto.Length - inicio);
            yield return texto.Substring(inicio, comprimento);
            if (inicio + comprimento >= texto.Length)
            {
                break;
            }

            inicio += Math.Max(1, comprimento - sobreposicao);
        }
    }
}
