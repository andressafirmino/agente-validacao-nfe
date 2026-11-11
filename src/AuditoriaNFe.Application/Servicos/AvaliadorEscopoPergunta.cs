using System.Globalization;
using System.Text;
using AuditoriaNFe.Domain.Sessoes;

namespace AuditoriaNFe.Application.Servicos;

/// <summary>
/// Classificação determinística de perguntas (FR-032): fora de auditoria de NF-e modelo 55 não
/// delega à etapa 7 quando o texto não indica tema fiscal ou de documentos da sessão.
/// </summary>
public static class AvaliadorEscopoPergunta
{
    private static readonly string[] TermosRelacionados =
    [
        "achado",
        "auditoria",
        "autoriza",
        "cancelamento",
        "cfop",
        "chave de acesso",
        "cnpj",
        "cofins",
        "csosn",
        "cst",
        "danfe",
        "destinat",
        "diverg",
        "emitent",
        "evento nfe",
        "fiscal",
        "icms",
        "icms-st",
        "imposto",
        "ipi",
        "issqn",
        "layout 4",
        "modelo 55",
        "nf-e",
        "nfe",
        "nota fiscal",
        "notas fiscais",
        "pis",
        "protocolo",
        "rejei",
        "schema",
        "sefaz",
        "sped",
        "totalizador",
        "tribut",
        "valida",
        "valor total",
        "vnf",
        "xml",
        "xsd",
        "lote",
    ];

    /// <summary>True quando a pergunta deve receber resposta fixa de fora do escopo, sem chamar o LLM.</summary>
    public static bool DeveResponderForaDoEscopoDeterministico(TipoExecucao tipo, string? pergunta)
    {
        if (string.IsNullOrWhiteSpace(pergunta))
        {
            return false;
        }

        if (tipo is not (TipoExecucao.Pergunta or TipoExecucao.AnaliseComPergunta))
        {
            return false;
        }

        return !RelacionadaAuditoriaNFe(pergunta);
    }

    private static readonly HashSet<string> PalavrasInteiras = new(StringComparer.Ordinal)
    {
        "nota",
        "notas",
    };

    internal static bool RelacionadaAuditoriaNFe(string pergunta)
    {
        var texto = Normalizar(pergunta);
        foreach (var termo in TermosRelacionados)
        {
            if (texto.Contains(Normalizar(termo), StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (var token in texto.Split([' ', ',', '.', '?', '!', ';', ':', '(', ')', '"', '\''], StringSplitOptions.RemoveEmptyEntries))
        {
            if (PalavrasInteiras.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalizar(string valor)
    {
        var minusculo = valor.Trim().ToLowerInvariant();
        var normalizado = minusculo.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizado.Length);
        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
