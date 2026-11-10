using AuditoriaNFe.Application.Servicos;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Parecer;
using AuditoriaNFe.Domain.Validacao;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Aplicacao;

/// <summary>
/// TS-073: descrição do achado de regra em linguagem natural (FR-017a) e resumo do sistema quando a
/// análise por IA não está disponível (FR-017b).
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class ResumoParecerTests
{
    private readonly AmbienteIntegracao _ambiente;

    public ResumoParecerTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Resumo_do_sistema_explica_inconsistencias_nao_analisados_e_alertas_TS073()
    {
        for (var i = 0; i < 10; i++)
        {
            _ambiente.ChatClient.Lancar(new HttpRequestException("Provedor indisponível."));
        }

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            [
                ArquivoEnvio.Fixture(NFeValida),
                ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente),
                ArquivoEnvio.Fixture(FixturesXml.Casos.EventoCancelamento),
            ],
            cancelamento: Cancelamento));

        var resumo = envelope.Parecer!.ResumoTextual;
        Assert.Contains("3 arquivos recebidos", resumo, StringComparison.Ordinal);
        Assert.Contains("2 analisados", resumo, StringComparison.Ordinal);
        Assert.Contains("1 não analisado", resumo, StringComparison.Ordinal);
        Assert.Contains("com inconsistências", resumo, StringComparison.Ordinal);
        Assert.Contains(Path.GetFileName(FixturesXml.Casos.TotalizadorDivergente), resumo, StringComparison.Ordinal);
        Assert.Contains("`total/ICMSTot/vICMS` informado 19.00, esperado 18.00", resumo, StringComparison.Ordinal);
        Assert.Contains("W04-10", resumo, StringComparison.Ordinal);
        Assert.Contains(Path.GetFileName(FixturesXml.Casos.EventoCancelamento), resumo, StringComparison.Ordinal);
        Assert.Contains("evento de NF-e", resumo, StringComparison.Ordinal);
        Assert.Contains("análise por IA", resumo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("achado", resumo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Descricao_do_achado_de_regra_comeca_pela_frase_em_linguagem_natural_TS073()
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(), [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)], cancelamento: Cancelamento));

        var descricao = AchadoDaRegra(envelope, "W04-10").Descricao;
        Assert.StartsWith("O campo `total/ICMSTot/vICMS` foi informado com 19.00, mas o valor esperado é 18.00", descricao, StringComparison.Ordinal);
        Assert.Contains("soma de `vICMS` dos itens", descricao, StringComparison.Ordinal);
        Assert.True(
            descricao.IndexOf("Regra W04-10 (versão 1)", StringComparison.Ordinal) > descricao.IndexOf("esperado", StringComparison.Ordinal),
            descricao);
    }

    [Fact]
    public void Resumo_lista_no_maximo_10_inconsistencias_e_informa_quantas_ficaram_de_fora_TS073()
    {
        var achados = Enumerable.Range(1, 12)
            .Select(i => new Achado(
                Guid.CreateVersion7(),
                CategoriaAchado.InconsistenciaNegocio,
                Severidade.Alta,
                OrigemAchado.Deterministica,
                $"descrição {i}",
                arquivo: "lote.xml",
                campo: $"det[{i}]/prod/vProd",
                valorInformado: "1.00",
                valorEsperado: "2.00",
                regraId: "W07-10",
                versaoRegra: 1))
            .ToList();

        var resumo = MontadorParecer.ResumoDoSistema(
            new ResumoQuantitativo(1, 1, 1, 0), ResultadoGeral.ComInconsistencias, Array.Empty<ArquivoRecebido>(), achados);

        Assert.Contains("det[10]/prod/vProd", resumo, StringComparison.Ordinal);
        Assert.DoesNotContain("det[11]/prod/vProd", resumo, StringComparison.Ordinal);
        Assert.Contains("mais 2 inconsistências", resumo, StringComparison.Ordinal);
        Assert.DoesNotContain("achado", resumo, StringComparison.OrdinalIgnoreCase);
    }
}
