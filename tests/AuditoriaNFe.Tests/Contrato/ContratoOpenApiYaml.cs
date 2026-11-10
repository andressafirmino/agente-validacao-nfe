using System.Text.RegularExpressions;
using AuditoriaNFe.Tests.Arquitetura;

namespace AuditoriaNFe.Tests.Contrato;

public sealed record OperacaoYaml(string Metodo, string Rota, string? OperationId, IReadOnlyList<string> Status, IReadOnlyList<string> CamposMultipart);

public sealed record EsquemaYaml(string Nome, IReadOnlySet<string> Requeridos, IReadOnlySet<string> Propriedades, bool SemAdicionais);

/// <summary>
/// Superfície de contracts/api-agente.openapi.yaml (operações, respostas, campos do multipart e
/// propriedades dos schemas de componentes), lida pela indentação de duas posições do arquivo.
/// </summary>
public static partial class ContratoOpenApiYaml
{
    private static readonly string[] Metodos = ["get", "post", "put", "delete", "patch"];

    public static string Caminho =>
        Path.Combine(Repositorio.Raiz(), "specs", "001-agente-auditoria-nfe", "contracts", "api-agente.openapi.yaml");

    public static IReadOnlyList<OperacaoYaml> Operacoes()
    {
        var operacoes = new List<OperacaoYaml>();
        string? rota = null;
        string? metodo = null;
        string? operationId = null;
        var status = new List<string>();
        var campos = new List<string>();
        var emRespostas = false;
        int? indentacaoMultipart = null;
        int? indentacaoPropriedades = null;

        void Fechar()
        {
            if (rota is not null && metodo is not null)
            {
                operacoes.Add(new OperacaoYaml(metodo, rota, operationId, [.. status], [.. campos]));
            }

            metodo = null;
            operationId = null;
            status.Clear();
            campos.Clear();
            emRespostas = false;
            indentacaoMultipart = null;
            indentacaoPropriedades = null;
        }

        foreach (var (_, indentacao, texto) in Linhas().SkipWhile(l => l.Texto != "paths:").Skip(1).TakeWhile(l => l.Indentacao > 0))
        {
            if (indentacao == 2 && texto.StartsWith('/'))
            {
                Fechar();
                rota = texto.TrimEnd(':');
                continue;
            }

            if (indentacao == 4 && Metodos.Contains(texto.TrimEnd(':')))
            {
                Fechar();
                metodo = texto.TrimEnd(':');
                continue;
            }

            if (indentacao == 6)
            {
                emRespostas = texto == "responses:";
                if (texto.StartsWith("operationId:", StringComparison.Ordinal))
                {
                    operationId = texto["operationId:".Length..].Trim();
                }
            }

            if (emRespostas && indentacao == 8 && StatusYaml().Match(texto) is { Success: true } codigo)
            {
                status.Add(codigo.Groups[1].Value);
            }

            if (texto == "multipart/form-data:")
            {
                indentacaoMultipart = indentacao;
                continue;
            }

            if (indentacaoMultipart is not { } baseMultipart)
            {
                continue;
            }

            if (indentacao <= baseMultipart)
            {
                indentacaoMultipart = null;
            }
            else if (indentacaoPropriedades is null && texto == "properties:")
            {
                indentacaoPropriedades = indentacao;
            }
            else if (indentacaoPropriedades is { } propriedades && indentacao <= propriedades)
            {
                indentacaoMultipart = null;
            }
            else if (indentacaoPropriedades is { } nivel && indentacao == nivel + 2 && Chave().Match(texto) is { Success: true } campo)
            {
                campos.Add(campo.Groups[1].Value);
            }
        }

        Fechar();
        return operacoes;
    }

    public static IReadOnlyDictionary<string, EsquemaYaml> Esquemas()
    {
        var esquemas = new Dictionary<string, EsquemaYaml>(StringComparer.Ordinal);
        string? nome = null;
        var requeridos = new HashSet<string>(StringComparer.Ordinal);
        var propriedades = new HashSet<string>(StringComparer.Ordinal);
        var semAdicionais = false;
        var emPropriedades = false;

        void Fechar()
        {
            if (nome is not null)
            {
                esquemas[nome] = new EsquemaYaml(nome, new HashSet<string>(requeridos), new HashSet<string>(propriedades), semAdicionais);
            }

            nome = null;
            requeridos.Clear();
            propriedades.Clear();
            semAdicionais = false;
            emPropriedades = false;
        }

        foreach (var (_, indentacao, texto) in Linhas().SkipWhile(l => l.Texto != "components:").SkipWhile(l => l.Texto != "schemas:").Skip(1).TakeWhile(l => l.Indentacao > 2))
        {
            if (indentacao == 4)
            {
                Fechar();
                nome = texto.TrimEnd(':');
                continue;
            }

            if (indentacao == 6)
            {
                emPropriedades = texto == "properties:";
                if (texto.StartsWith("required:", StringComparison.Ordinal))
                {
                    requeridos.UnionWith(texto["required:".Length..].Trim().Trim('[', ']').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                }

                semAdicionais |= texto == "additionalProperties: false";
            }

            if (emPropriedades && indentacao == 8 && Chave().Match(texto) is { Success: true } propriedade)
            {
                propriedades.Add(propriedade.Groups[1].Value);
            }
        }

        Fechar();
        return esquemas;
    }

    private static IEnumerable<(string Linha, int Indentacao, string Texto)> Linhas() =>
        File.ReadLines(Caminho)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .Select(l => (l, l.Length - l.TrimStart(' ').Length, l.Trim()));

    [GeneratedRegex(@"^'(\d{3})':$")]
    private static partial Regex StatusYaml();

    [GeneratedRegex(@"^([A-Za-z_][A-Za-z0-9_]*):")]
    private static partial Regex Chave();
}
