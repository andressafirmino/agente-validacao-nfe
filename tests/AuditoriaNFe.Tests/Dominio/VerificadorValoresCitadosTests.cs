using AuditoriaNFe.Domain.Calculos;
using AuditoriaNFe.Domain.Rastreabilidade;

namespace AuditoriaNFe.Tests.Dominio;

/// <summary>Rastreabilidade de valores citados (FR-027, FR-028, T108).</summary>
public sealed class VerificadorValoresCitadosTests
{
    private const string CampoVnf = "total/ICMSTot/vNF";
    private const string Chave = "35261111222333000181550010000009081071904650";
    private static readonly DateTimeOffset Agora = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valor_com_calculoId_igual_ao_resultado_valida()
    {
        var calculoId = Guid.CreateVersion7();
        var calculos = new Dictionary<Guid, RegistroCalculo>
        {
            [calculoId] = new(
                calculoId,
                OperacaoCalculo.Soma,
                CampoVnf,
                [],
                [],
                1500.75m,
                Agora),
        };

        var citados = new[] { new ValorCitado("1500.75", calculoId, null, null) };
        var problemas = VerificadorValoresCitados.Verificar(
            "O total da base é 1.500,75.",
            citados,
            calculos,
            CamposVazios());

        Assert.Empty(problemas);
    }

    [Fact]
    public void Valor_com_chave_e_caminho_igual_ao_extraido_valida()
    {
        var campos = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            [Chave] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CampoVnf] = "1234.56",
            },
        };

        var citados = new[] { new ValorCitado("1234.56", null, Chave, CampoVnf) };
        var problemas = VerificadorValoresCitados.Verificar(
            "O vNF da nota citada é R$ 1.234,56.",
            citados,
            CalculosVazios(),
            campos);

        Assert.Empty(problemas);
    }

    [Fact]
    public void Valor_monetario_no_texto_sem_valoresCitados_rejeita()
    {
        var problemas = VerificadorValoresCitados.Verificar(
            "O total somado foi R$ 99,90.",
            [],
            CalculosVazios(),
            CamposVazios());

        Assert.Contains(problemas, p => p.Contains("formato monetário", StringComparison.Ordinal));
    }

    [Fact]
    public void CalculoId_inexistente_ou_valor_diferente_rejeita()
    {
        var calculoId = Guid.CreateVersion7();
        var outroId = Guid.CreateVersion7();
        var calculos = new Dictionary<Guid, RegistroCalculo>
        {
            [calculoId] = new(calculoId, OperacaoCalculo.Soma, CampoVnf, [], [], 100m, Agora),
        };

        var citadosInexistente = new[] { new ValorCitado("100", outroId, null, null) };
        Assert.Contains(
            VerificadorValoresCitados.Verificar("Total 100,00.", citadosInexistente, calculos, CamposVazios()),
            p => p.Contains("calculoId não existe", StringComparison.Ordinal));

        var citadosDiferente = new[] { new ValorCitado("200", calculoId, null, null) };
        Assert.Contains(
            VerificadorValoresCitados.Verificar("Total 200,00.", citadosDiferente, calculos, CamposVazios()),
            p => p.Contains("não confere com o resultado", StringComparison.Ordinal));
    }

    [Fact]
    public void Campo_extraido_inexistente_ou_valor_divergente_rejeita()
    {
        var campos = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            [Chave] = new Dictionary<string, string>(StringComparer.Ordinal) { [CampoVnf] = "10.00" },
        };

        var citadoCaminhoInvalido = new[] { new ValorCitado("10.00", null, Chave, "total/inexistente") };
        Assert.Contains(
            VerificadorValoresCitados.Verificar("Valor 10,00.", citadoCaminhoInvalido, CalculosVazios(), campos),
            p => p.Contains("não existe nos dados extraídos", StringComparison.Ordinal));

        var citadoValorInvalido = new[] { new ValorCitado("99.00", null, Chave, CampoVnf) };
        Assert.Contains(
            VerificadorValoresCitados.Verificar("Valor 99,00.", citadoValorInvalido, CalculosVazios(), campos),
            p => p.Contains("não existe nos dados extraídos", StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<Guid, RegistroCalculo> CalculosVazios() =>
        new Dictionary<Guid, RegistroCalculo>();

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> CamposVazios() =>
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
}
