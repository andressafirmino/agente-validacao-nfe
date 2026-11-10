using System.Globalization;
using System.Xml.Linq;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Infrastructure.Catalogos;

namespace AuditoriaNFe.Infrastructure.Xml;

/// <summary>
/// Extrai de <c>infNFe</c> só os campos do catálogo de extração, cada um com o caminho no XML
/// (FR-037a): itens como <c>det[nItem]</c> e demais ocorrências repetidas do catálogo (<c>dup</c>,
/// <c>detPag</c>) pela posição, a partir de 1. O XML não é guardado.
/// </summary>
public sealed class ExtratorDados
{
    private readonly IReadOnlyList<string> _padroes;
    private readonly HashSet<string> _indexados;

    public ExtratorDados(IEnumerable<CampoCatalogoExtracao> campos)
    {
        ArgumentNullException.ThrowIfNull(campos);
        _padroes = [.. campos.Select(c => c.Caminho)];
        _indexados = _padroes
            .SelectMany(p => p.Split('/'))
            .Where(s => s.EndsWith("[*]", StringComparison.Ordinal))
            .Select(s => s[..^3])
            .ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<CampoExtraido> Extrair(XDocument documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var infNFe = IdentificadorNFe.InfNFe(documento);
        if (infNFe is null)
        {
            return [];
        }

        var campos = new List<CampoExtraido>();
        Percorrer(infNFe, string.Empty, campos);
        return campos;
    }

    private void Percorrer(XElement elemento, string prefixo, List<CampoExtraido> campos)
    {
        var posicoes = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var filho in elemento.Elements())
        {
            var nome = filho.Name.LocalName;
            var segmento = nome;
            if (_indexados.Contains(nome))
            {
                var posicao = posicoes[nome] = posicoes.GetValueOrDefault(nome) + 1;
                var indice = nome == "det" && int.TryParse((string?)filho.Attribute("nItem"), NumberStyles.None, CultureInfo.InvariantCulture, out var nItem)
                    ? nItem
                    : posicao;
                segmento = $"{nome}[{indice.ToString(CultureInfo.InvariantCulture)}]";
            }

            var caminho = prefixo.Length == 0 ? segmento : $"{prefixo}/{segmento}";
            if (filho.HasElements)
            {
                if (_padroes.Any(p => PodeConter(p, caminho)))
                {
                    Percorrer(filho, caminho, campos);
                }
            }
            else if (_padroes.Any(p => CaminhoCatalogo.Corresponde(p, caminho)))
            {
                campos.Add(new CampoExtraido(caminho, filho.Value));
            }
        }
    }

    /// <summary>O padrão tem mais segmentos que o caminho e começa por ele.</summary>
    private static bool PodeConter(string padrao, string caminho)
    {
        var segmentos = caminho.Split('/');
        var segmentosPadrao = padrao.Split('/');
        return segmentosPadrao.Length > segmentos.Length
            && CaminhoCatalogo.Corresponde(string.Join('/', segmentosPadrao.Take(segmentos.Length)), caminho);
    }
}
