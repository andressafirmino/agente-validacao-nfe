using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using AuditoriaNFe.Tests.Arquitetura;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;

namespace AuditoriaNFe.Tests.Avaliacao;

public sealed record ItemAvaliacao(string Descricao, decimal Quantidade, decimal ValorUnitario, string? Cfop = null);

public sealed record DefinicaoAvaliacao(string Caminho, string Valor);

/// <summary>Um destino por inserção: <c>substituir</c>, <c>anexarEm</c>, <c>depoisDe</c> ou <c>antesDe</c>.</summary>
public sealed record InsercaoAvaliacao(string Xml, string? Substituir = null, string? AnexarEm = null, string? DepoisDe = null, string? AntesDe = null);

public sealed record DocumentoAvaliacao(
    int Numero,
    IReadOnlyList<ItemAvaliacao>? Itens = null,
    IReadOnlyList<DefinicaoAvaliacao>? Definir = null,
    IReadOnlyList<InsercaoAvaliacao>? Inserir = null);

public sealed record LoteAvaliacao(string Id, bool SemDivergencia, IReadOnlyList<string> RegrasDivergentes, IReadOnlyList<DocumentoAvaliacao> Documentos)
{
    public IReadOnlyList<ArquivoEnvio> Arquivos() =>
        [.. Documentos.Select(d => new ArquivoEnvio($"{Id}-{d.Numero}.xml", ConjuntoAvaliacaoAnalise.GerarBytes(d)))];
}

public sealed record ConjuntoAvaliacao(string Versao, string Descricao, IReadOnlyList<LoteAvaliacao> Lotes);

/// <summary>Lê tests/AuditoriaNFe.Tests/Avaliacao/conjunto-avaliacao-analise.v1.json e monta as NF-e dos lotes.</summary>
public static class ConjuntoAvaliacaoAnalise
{
    private static readonly XNamespace Nfe = GeradorNFeSintetica.Nfe;

    public static string Caminho =>
        Path.Combine(Repositorio.Raiz(), "tests", "AuditoriaNFe.Tests", "Avaliacao", "conjunto-avaliacao-analise.v1.json");

    public static ConjuntoAvaliacao Carregar() =>
        JsonSerializer.Deserialize<ConjuntoAvaliacao>(File.ReadAllText(Caminho), new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        }) ?? throw new InvalidOperationException("Conjunto de avaliação vazio.");

    public static XDocument Gerar(DocumentoAvaliacao documento)
    {
        var opcoes = new OpcoesNFeSintetica { Numero = documento.Numero, Ajustar = xml => Ajustar(xml, documento) };
        if (documento.Itens is { Count: > 0 } itens)
        {
            opcoes = opcoes with
            {
                Itens = [.. itens.Select(i => i.Cfop is null
                    ? new ItemSintetico(i.Descricao, i.Quantidade, i.ValorUnitario)
                    : new ItemSintetico(i.Descricao, i.Quantidade, i.ValorUnitario, Cfop: i.Cfop))],
            };
        }

        return GeradorNFeSintetica.Gerar(opcoes);
    }

    public static byte[] GerarBytes(DocumentoAvaliacao documento)
    {
        var xml = Gerar(documento);
        return System.Text.Encoding.UTF8.GetBytes(xml.Declaration + xml.ToString(SaveOptions.DisableFormatting));
    }

    private static void Ajustar(XDocument xml, DocumentoAvaliacao documento)
    {
        var infNFe = xml.Descendants(Nfe + "infNFe").Single();
        foreach (var insercao in documento.Inserir ?? [])
        {
            var fragmento = Fragmento(insercao.Xml);
            if (insercao.Substituir is { } substituir)
            {
                Elemento(infNFe, substituir).ReplaceWith(fragmento);
            }
            else if (insercao.AnexarEm is { } pai)
            {
                Elemento(infNFe, pai).Add(fragmento);
            }
            else if (insercao.DepoisDe is { } anterior)
            {
                Elemento(infNFe, anterior).AddAfterSelf(fragmento);
            }
            else if (insercao.AntesDe is { } posterior)
            {
                Elemento(infNFe, posterior).AddBeforeSelf(fragmento);
            }
            else
            {
                throw new InvalidOperationException($"Inserção sem destino no documento {documento.Numero}.");
            }
        }

        foreach (var definicao in documento.Definir ?? [])
        {
            Elemento(infNFe, definicao.Caminho).Value = definicao.Valor;
        }
    }

    private static XElement[] Fragmento(string xml) =>
        [.. XElement.Parse($"<r xmlns=\"{Nfe.NamespaceName}\">{xml}</r>").Elements()];

    /// <summary><c>det[n]</c> pelo <c>nItem</c>; outros índices pela posição (base 1).</summary>
    private static XElement Elemento(XElement infNFe, string caminho)
    {
        var atual = infNFe;
        foreach (var parte in caminho.Split('/'))
        {
            var abre = parte.IndexOf('[', StringComparison.Ordinal);
            var nome = abre < 0 ? parte : parte[..abre];
            var indice = abre < 0 ? (int?)null : int.Parse(parte[(abre + 1)..^1], CultureInfo.InvariantCulture);
            var candidatos = atual.Elements(Nfe + nome);
            atual = indice switch
            {
                null => candidatos.Single(),
                { } n when nome == "det" => candidatos.Single(e => (string?)e.Attribute("nItem") == n.ToString(CultureInfo.InvariantCulture)),
                { } n => candidatos.ElementAt(n - 1),
            };
        }

        return atual;
    }
}
