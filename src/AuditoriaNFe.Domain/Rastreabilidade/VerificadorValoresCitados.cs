using System.Globalization;
using System.Text.RegularExpressions;
using AuditoriaNFe.Domain.Calculos;

namespace AuditoriaNFe.Domain.Rastreabilidade;

/// <summary>Validação semântica de <c>valoresCitados</c> e do texto da resposta (data-model §15, FR-027, FR-028).</summary>
public static partial class VerificadorValoresCitados
{
    public static IReadOnlyList<string> Verificar(
        string texto,
        IReadOnlyList<ValorCitado> valoresCitados,
        IReadOnlyDictionary<Guid, RegistroCalculo> calculosPorId,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> camposPorChave)
    {
        ArgumentNullException.ThrowIfNull(texto);
        ArgumentNullException.ThrowIfNull(valoresCitados);
        ArgumentNullException.ThrowIfNull(calculosPorId);
        ArgumentNullException.ThrowIfNull(camposPorChave);

        var problemas = new List<string>();

        for (var i = 0; i < valoresCitados.Count; i++)
        {
            var citado = valoresCitados[i];
            var rotulo = $"valoresCitados[{i}]";

            if (!FormatoDecimalInvariante().IsMatch(citado.Valor))
            {
                problemas.Add($"{rotulo}: valor fora do formato decimal.");
                continue;
            }

            if (citado.CalculoId is { } calculoId)
            {
                if (!calculosPorId.TryGetValue(calculoId, out var registro))
                {
                    problemas.Add($"{rotulo}: calculoId não existe na execução.");
                }
                else if (!MesmoValorDecimal(registro.Resultado, citado.Valor))
                {
                    problemas.Add($"{rotulo}: valor não confere com o resultado do cálculo.");
                }

                continue;
            }

            if (citado.ChaveAcesso is { } chave && citado.Caminho is { } caminho)
            {
                if (!camposPorChave.TryGetValue(chave, out var campos)
                    || !campos.TryGetValue(caminho, out var extraido)
                    || !MesmoValorTexto(extraido, citado.Valor))
                {
                    problemas.Add($"{rotulo}: o campo citado não existe nos dados extraídos com esse valor.");
                }

                continue;
            }

            problemas.Add($"{rotulo}: exige calculoId ou par chaveAcesso/caminho.");
        }

        var citadosNormalizados = valoresCitados
            .Select(v => NormalizarCitado(v.Valor))
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToHashSet();

        foreach (var numero in ValoresMonetariosNoTexto(texto))
        {
            if (!citadosNormalizados.Contains(numero))
            {
                problemas.Add("resposta.texto: há valor em formato monetário sem correspondente em valoresCitados.");
                break;
            }
        }

        return problemas;
    }

    private static IEnumerable<decimal> ValoresMonetariosNoTexto(string texto) =>
        ValorMonetario().Matches(texto).Select(m => NormalizarTexto(m.Value)).Where(v => v.HasValue).Select(v => v!.Value);

    private static decimal? NormalizarCitado(string valor) =>
        decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero) ? numero : null;

    private static decimal? NormalizarTexto(string token)
    {
        var texto = token.Replace("R$", string.Empty, StringComparison.Ordinal).Trim();
        if (texto.Contains(',', StringComparison.Ordinal))
        {
            texto = texto.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }

        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero) ? numero : null;
    }

    private static bool MesmoValorDecimal(decimal resultado, string citado) =>
        decimal.TryParse(citado, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero) && numero == resultado;

    private static bool MesmoValorTexto(string extraido, string citado) =>
        string.Equals(extraido, citado, StringComparison.Ordinal)
        || (decimal.TryParse(extraido, NumberStyles.Number, CultureInfo.InvariantCulture, out var a)
            && decimal.TryParse(citado, NumberStyles.Number, CultureInfo.InvariantCulture, out var b)
            && a == b);

    [GeneratedRegex(@"^-?[0-9]+(\.[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDecimalInvariante();

    [GeneratedRegex(@"(?:R\$\s?)?\d{1,3}(?:\.\d{3})*,\d{2}|\d+\.\d{2}(?!\d)")]
    private static partial Regex ValorMonetario();
}
