using System.Globalization;
using System.Text.RegularExpressions;

namespace AuditoriaNFe.Domain.Regras;

/// <summary>
/// Descrição de uma divergência de regra (FR-017a): frase em linguagem natural com campo, valor
/// informado e esperado e, nas regras de soma dos itens, o que a regra exige; depois, identificador,
/// versão e fórmula do catálogo. O texto vem só do catálogo e da divergência.
/// </summary>
public static partial class DescricaoAchadoRegra
{
    public static string Montar(RegraConteudo regra, DivergenciaRegra divergencia)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentNullException.ThrowIfNull(divergencia);

        var item = divergencia.Item is { } numero ? $" no item {numero.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        var frase = Numero(divergencia.ValorInformado) && Numero(divergencia.ValorEsperado)
            ? $"O campo `{divergencia.Campo}`{item} foi informado com {divergencia.ValorInformado}, mas o valor esperado é {divergencia.ValorEsperado}"
            : $"O campo `{divergencia.Campo}`{item} está {divergencia.ValorInformado}, mas deveria estar {divergencia.ValorEsperado}";
        var formula = regra.Descricao.Trim().TrimEnd('.');
        var exigencia = SomaDosItens().Match(formula) is { Success: true } soma
            ? $": deve ser igual à soma de `{soma.Groups["parcela"].Value}` {Itens(soma.Groups["resto"].Value)}"
            : string.Empty;

        return $"{frase}{exigencia}. Regra {regra.Id} (versão {regra.VersaoRegra.ToString(CultureInfo.InvariantCulture)}): {formula}.";
    }

    private static string Itens(string resto) =>
        resto.TrimStart().StartsWith("dos itens", StringComparison.Ordinal) ? resto.Trim() : ("dos itens" + resto).TrimEnd();

    private static bool Numero(string valor) =>
        decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out _);

    [GeneratedRegex(@"^\S+ \([^)]+\) = Σ itens (?<parcela>\S+) \([^)]+\)(?<resto>.*)$")]
    private static partial Regex SomaDosItens();
}
