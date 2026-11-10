using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Regras;
using AuditoriaNFe.Tests.Dominio.Regras;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Avaliacao;

/// <summary>Estrutura do conjunto de avaliação da análise: roda em toda execução da suíte, sem IA.</summary>
public sealed class ConjuntoAvaliacaoAnaliseTests
{
    private static readonly ConjuntoAvaliacao Conjunto = ConjuntoAvaliacaoAnalise.Carregar();

    public static TheoryData<string> Lotes => [.. Conjunto.Lotes.Select(l => l.Id)];

    [Fact]
    public void Conjunto_tem_ao_menos_20_lotes_com_ids_unicos_e_lotes_sem_divergencia()
    {
        Assert.Equal("1.0", Conjunto.Versao);
        Assert.True(Conjunto.Lotes.Count >= 20, $"São {Conjunto.Lotes.Count} lotes.");
        Assert.Equal(Conjunto.Lotes.Count, Conjunto.Lotes.Select(l => l.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(Conjunto.Lotes, l => l.SemDivergencia);
        Assert.All(Conjunto.Lotes.Where(l => l.SemDivergencia), l => Assert.Empty(l.RegrasDivergentes));
    }

    [Fact]
    public void Ha_divergencia_conhecida_em_cada_grupo_de_regra()
    {
        var grupos = Conjunto.Lotes.SelectMany(l => l.RegrasDivergentes).Select(r => CatalogoRegrasTeste.Versoes(r)[0].Grupo).ToHashSet();

        Assert.Equal(Enum.GetValues<GrupoRegra>().Order(), grupos.Order());
    }

    [Theory]
    [MemberData(nameof(Lotes))]
    public void Documentos_do_lote_sao_validos_no_schema_do_pacote(string loteId)
    {
        var lote = Conjunto.Lotes.Single(l => l.Id == loteId);

        Assert.All(lote.Documentos, documento =>
            Assert.Empty(ValidacaoXsdTeste.Validar(ConjuntoAvaliacaoAnalise.Gerar(documento), FixturesXml.Casos.Pacote, "procNFe_v4.00.xsd")));
        Assert.Equal(lote.Documentos.Count, lote.Documentos.Select(d => d.Numero).Distinct().Count());
    }
}

/// <summary>
/// Avaliação da análise por IA com a OpenAI real, sob demanda (<c>--filter-trait "Categoria=Avaliacao"</c>,
/// com <c>OpenAI__ApiKey</c> e <c>OpenAI__Modelo</c> no ambiente). Critérios do plano (P17): 0 indícios
/// sem evidência verificável, 0 indícios que contradizem ou repetem como novo um achado determinístico
/// e 0 valores não rastreáveis no resumo.
/// </summary>
[Trait("Categoria", "Avaliacao")]
[Collection(ColecaoIntegracao.Nome)]
public sealed partial class AvaliacaoAnaliseTests(AmbienteIntegracao ambiente)
{
    private static readonly string[] TermosDeContestacao = ["falso positivo", "desconsider", "não procede", "nao procede", "ignorar", "incorreto o achado"];

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Analise_por_IA_atende_os_criterios_do_plano_no_conjunto_de_avaliacao()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OpenAI__ApiKey"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OpenAI__Modelo")))
        {
            Assert.Skip("Avaliação sob demanda: defina OpenAI__ApiKey e OpenAI__Modelo.");
        }

        var cliente = ambiente.CriarFabrica(provedorReal: true).CreateClient();
        var regrasDoCatalogo = CatalogoRegrasTeste.Todas.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var semEvidencia = new List<string>();
        var contraDeterministico = new List<string>();
        var naoRastreaveis = new List<string>();
        var deterministicosIncorretos = new List<string>();

        foreach (var lote in ConjuntoAvaliacaoAnalise.Carregar().Lotes)
        {
            var envelope = Concluida(await ClienteExecucoes.CriarAsync(cliente, lote.Arquivos(), cancelamento: Cancelamento));
            var parecer = envelope.Parecer!;
            var chaves = parecer.Documentos.Select(d => d.ChaveAcesso).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var deterministicos = parecer.Achados.Where(a => a.Origem == "Deterministica").ToList();
            var regrasDivergentes = deterministicos.Select(a => a.RegraId).OfType<string>().ToHashSet(StringComparer.Ordinal);

            var faltantes = lote.RegrasDivergentes.Where(r => !regrasDivergentes.Contains(r)).ToList();
            if (faltantes.Count > 0 || (lote.SemDivergencia && parecer.ResultadoGeral != "SemInconsistencias"))
            {
                deterministicosIncorretos.Add($"{lote.Id}: esperadas [{string.Join(", ", lote.RegrasDivergentes)}], obtidas [{string.Join(", ", regrasDivergentes)}], {parecer.ResultadoGeral}");
            }

            foreach (var indicio in parecer.Achados.Where(a => a.Origem == "IA" && a.Categoria == "IndicioIa"))
            {
                if (indicio.Evidencias.Count == 0 || indicio.Evidencias.Any(e => !Verificavel(e, chaves, regrasDoCatalogo)))
                {
                    semEvidencia.Add($"{lote.Id}: {indicio.Descricao}");
                }

                var regrasCitadas = indicio.Evidencias.Select(e => e.RegraId).OfType<string>()
                    .Concat(regrasDivergentes.Where(r => indicio.Descricao.Contains(r, StringComparison.OrdinalIgnoreCase)));
                if (regrasCitadas.Any(regrasDivergentes.Contains)
                    || TermosDeContestacao.Any(t => indicio.Descricao.Contains(t, StringComparison.OrdinalIgnoreCase)))
                {
                    contraDeterministico.Add($"{lote.Id}: {indicio.Descricao}");
                }
            }

            var rastreaveis = parecer.Achados
                .SelectMany(a => new[] { a.ValorInformado, a.ValorEsperado }.Concat(a.Evidencias.Select(e => e.Valor)))
                .OfType<string>().Select(Normalizar).OfType<decimal>().ToHashSet();
            naoRastreaveis.AddRange(ValoresMonetarios(parecer.ResumoTextual).Where(v => !rastreaveis.Contains(v)).Select(v => $"{lote.Id}: {v}"));
        }

        var relatorio = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Indícios sem evidência verificável: {semEvidencia.Count}").AppendJoin('\n', semEvidencia).AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Indícios que contradizem ou repetem achado determinístico: {contraDeterministico.Count}").AppendJoin('\n', contraDeterministico).AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Valores não rastreáveis no resumo: {naoRastreaveis.Count}").AppendJoin('\n', naoRastreaveis).AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Lotes com achados determinísticos diferentes do esperado: {deterministicosIncorretos.Count}").AppendJoin('\n', deterministicosIncorretos)
            .ToString();
        TestContext.Current.SendDiagnosticMessage(relatorio);

        Assert.True(semEvidencia.Count == 0 && contraDeterministico.Count == 0 && naoRastreaveis.Count == 0 && deterministicosIncorretos.Count == 0, relatorio);
    }

    private static bool Verificavel(EvidenciaV1 evidencia, IReadOnlySet<string> chaves, IReadOnlySet<string> regras) => evidencia.Tipo switch
    {
        "CampoXml" => evidencia.ChaveAcesso is { } chave && chaves.Contains(chave)
            && !string.IsNullOrWhiteSpace(evidencia.Caminho) && !string.IsNullOrWhiteSpace(evidencia.Valor),
        "RegraCatalogo" => evidencia.RegraId is { } regra && regras.Contains(regra),
        _ => false,
    };

    private static IEnumerable<decimal> ValoresMonetarios(string texto) =>
        ValorMonetario().Matches(texto).Select(m => Normalizar(m.Value)).OfType<decimal>();

    private static decimal? Normalizar(string valor)
    {
        var texto = valor.Replace("R$", string.Empty, StringComparison.Ordinal).Trim();
        if (texto.Contains(',', StringComparison.Ordinal))
        {
            texto = texto.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }

        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero) ? numero : null;
    }

    [GeneratedRegex(@"(?:R\$\s?)?\d{1,3}(?:\.\d{3})*,\d{2}|\d+\.\d{2}(?!\d)")]
    private static partial Regex ValorMonetario();
}
