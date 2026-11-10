using System.Text.RegularExpressions;
using AuditoriaNFe.Application.ContextProviders;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Application.Servicos;

/// <summary>
/// Minimização dos dados enviados ao provedor de IA (ADR-0004): só os campos extraídos da lista
/// versionada abaixo e os resultados determinísticos; CPF, e-mails e telefones mascarados em todo
/// texto; descrição de erro de schema substituída pelo caminho, porque a mensagem do validador pode
/// repetir o valor do campo. O XML integral nunca é enviado.
/// </summary>
public static partial class MinimizadorDadosIa
{
    public const string VersaoListaCampos = "1";
    public const string Mascara = "[removido]";

    /// <summary>Prefixos permitidos do caminho relativo a <c>infNFe</c>, com os índices como <c>[*]</c>.</summary>
    private static readonly string[] PrefixosPermitidos =
    [
        "ide/",
        "total/",
        "det[*]/prod/",
        "det[*]/imposto/",
        "cobr/",
        "pag/",
    ];

    private static readonly HashSet<string> CaminhosPermitidos = new(StringComparer.Ordinal)
    {
        "emit/CNPJ",
        "emit/enderEmit/UF",
        "dest/CNPJ",
        "dest/enderDest/UF",
    };

    public static bool CampoPermitido(string caminho)
    {
        ArgumentNullException.ThrowIfNull(caminho);
        var normalizado = Indice().Replace(caminho, "[*]");
        return CaminhosPermitidos.Contains(normalizado)
            || PrefixosPermitidos.Any(p => normalizado.StartsWith(p, StringComparison.Ordinal));
    }

    public static IReadOnlyList<CampoContextoIa> Campos(IEnumerable<CampoExtraido> campos)
    {
        ArgumentNullException.ThrowIfNull(campos);
        return [.. campos.Where(c => CampoPermitido(c.Caminho)).Select(c => new CampoContextoIa(c.Caminho, Mascarar(c.Valor)))];
    }

    public static AchadoContextoIa Achado(Achado achado)
    {
        ArgumentNullException.ThrowIfNull(achado);
        var descricao = achado.Categoria == CategoriaAchado.ErroEstrutural
            ? $"Violação de schema{(achado.Campo is null ? string.Empty : $" em {achado.Campo}")}{(achado.Linha is null ? string.Empty : $", linha {achado.Linha}")}."
            : Mascarar(achado.Descricao);
        return new AchadoContextoIa(
            achado.Id,
            achado.Categoria.ToString(),
            achado.Severidade.ToString(),
            descricao,
            achado.Arquivo is null ? null : Mascarar(achado.Arquivo),
            achado.Campo,
            achado.ValorInformado is null ? null : Mascarar(achado.ValorInformado),
            achado.ValorEsperado is null ? null : Mascarar(achado.ValorEsperado),
            achado.RegraId);
    }

    /// <summary>Substitui CPF, e-mail e telefone por <see cref="Mascara"/>.</summary>
    public static string Mascarar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        var resultado = Email().Replace(texto, Mascara);
        resultado = CpfFormatado().Replace(resultado, Mascara);
        resultado = TelefoneFormatado().Replace(resultado, Mascara);
        return SequenciaDeDezOuOnzeDigitos().Replace(resultado, Mascara);
    }

    [GeneratedRegex(@"\[\d+\]", RegexOptions.CultureInvariant)]
    private static partial Regex Indice();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Email();

    [GeneratedRegex(@"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CpfFormatado();

    [GeneratedRegex(@"\(?\b\d{2}\)?\s?9?\d{4}-\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex TelefoneFormatado();

    /// <summary>CPF e telefone sem formatação; chave de acesso (44) e CNPJ (14) não são afetados.</summary>
    [GeneratedRegex(@"(?<![\d.,])\d{10,11}(?![\d.,])", RegexOptions.CultureInvariant)]
    private static partial Regex SequenciaDeDezOuOnzeDigitos();
}
