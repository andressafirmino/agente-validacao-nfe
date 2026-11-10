using System.Globalization;
using System.Text.RegularExpressions;
using AuditoriaNFe.Domain.Documentos;

namespace AuditoriaNFe.Domain.Regras;

/// <summary>Acesso aos campos de <see cref="DadosDocumento"/> pelas regras, com os itens agrupados pelo <c>nItem</c>.</summary>
internal sealed partial class LeituraDocumento
{
    private readonly Dictionary<string, string> _campos;

    private LeituraDocumento(Dictionary<string, string> campos, IReadOnlyList<ItemDocumento> itens)
    {
        _campos = campos;
        Itens = itens;
    }

    public IReadOnlyList<ItemDocumento> Itens { get; }

    public static LeituraDocumento De(DadosDocumento documento)
    {
        var campos = new Dictionary<string, string>(StringComparer.Ordinal);
        var itens = new SortedDictionary<int, Dictionary<string, string>>();
        foreach (var campo in documento.Campos)
        {
            var item = CaminhoItem().Match(campo.Caminho);
            if (item.Success)
            {
                var numero = int.Parse(item.Groups["n"].Value, CultureInfo.InvariantCulture);
                if (!itens.TryGetValue(numero, out var camposItem))
                {
                    camposItem = new Dictionary<string, string>(StringComparer.Ordinal);
                    itens[numero] = camposItem;
                }

                camposItem.TryAdd(item.Groups["resto"].Value, campo.Valor);
            }
            else
            {
                campos.TryAdd(campo.Caminho, campo.Valor);
            }
        }

        return new LeituraDocumento(campos, [.. itens.Select(i => new ItemDocumento(i.Key, i.Value))]);
    }

    public bool Tem(string caminho) => _campos.ContainsKey(caminho);

    public bool TemGrupo(string prefixo) => _campos.Keys.Any(c => c.StartsWith(prefixo, StringComparison.Ordinal));

    public string? Texto(string caminho) => _campos.GetValueOrDefault(caminho);

    public decimal? Valor(string caminho) => Numero(Texto(caminho));

    /// <summary>Campo opcional ausente vale zero.</summary>
    public decimal ValorOuZero(string caminho) => Valor(caminho) ?? 0m;

    /// <summary>Soma dos campos fora dos itens que correspondem ao padrão, por exemplo <c>pag/detPag[*]/vPag</c>.</summary>
    public decimal Soma(string padrao) =>
        _campos.Where(c => CaminhoCatalogo.Corresponde(padrao, c.Key)).Sum(c => Numero(c.Value) ?? 0m);

    public bool Existe(string padrao) => _campos.Keys.Any(c => CaminhoCatalogo.Corresponde(padrao, c));

    public decimal SomaItens(string padraoItem, Func<ItemDocumento, bool>? filtro = null) =>
        Itens.Where(i => filtro?.Invoke(i) ?? true).Sum(i => i.Soma(padraoItem));

    internal static decimal? Numero(string? texto) =>
        decimal.TryParse(texto, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;

    [GeneratedRegex(@"\Adet\[(?<n>[0-9]+)\]/(?<resto>.+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex CaminhoItem();
}

/// <summary>Campos de um item (<c>det</c>), com caminhos relativos ao item: <c>prod/vProd</c>, <c>imposto/ICMS/ICMS00/vICMS</c>.</summary>
internal sealed class ItemDocumento(int numero, IReadOnlyDictionary<string, string> campos)
{
    public int Numero { get; } = numero;

    public bool TemGrupo(string prefixo) => campos.Keys.Any(c => c.StartsWith(prefixo, StringComparison.Ordinal));

    /// <summary>Primeiro valor que corresponde ao padrão, por exemplo <c>imposto/ICMS/*/CST</c>.</summary>
    public string? Texto(string padrao) =>
        campos.Where(c => CaminhoCatalogo.Corresponde(padrao, c.Key)).Select(c => c.Value).FirstOrDefault();

    public decimal Soma(string padrao) =>
        campos.Where(c => CaminhoCatalogo.Corresponde(padrao, c.Key)).Sum(c => LeituraDocumento.Numero(c.Value) ?? 0m);
}
