using System.Net;
using System.Text.Json;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>
/// O JSON devolvido pelo canal direto segue os schemas <c>EnvelopeV1</c>, <c>Parecer</c>,
/// <c>Achado</c>, <c>DocumentoParecer</c>, <c>Etapa</c>, <c>Erro</c> e <c>ErroResposta</c> de
/// contracts/api-agente.openapi.yaml: propriedades obrigatórias presentes e nenhuma fora do schema.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class EnvelopeV1ContratoTests
{
    private readonly AmbienteIntegracao _ambiente;
    private readonly IReadOnlyDictionary<string, EsquemaYaml> _esquemas = ContratoOpenApiYaml.Esquemas();

    public EnvelopeV1ContratoTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Envelope_concluido_segue_os_schemas_do_contrato()
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        var resposta = await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(),
            [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente), ArquivoEnvio.Fixture(FixturesXml.Casos.ViolacaoSchema)],
            cancelamento: Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.Status);
        using var json = JsonDocument.Parse(resposta.Corpo);
        var raiz = json.RootElement;
        Conforme(raiz, "EnvelopeV1");
        Assert.Equal("1.0", raiz.GetProperty("versaoEnvelope").GetString());
        Assert.Equal("Direto", raiz.GetProperty("canal").GetString());
        Assert.All(raiz.GetProperty("etapas").EnumerateArray(), e => Conforme(e, "Etapa"));
        var parecer = raiz.GetProperty("parecer");
        Conforme(parecer, "Parecer");
        Assert.All(parecer.GetProperty("documentos").EnumerateArray(), d => Conforme(d, "DocumentoParecer"));
        Assert.NotEmpty(parecer.GetProperty("achados").EnumerateArray());
        Assert.All(parecer.GetProperty("achados").EnumerateArray(), a =>
        {
            Conforme(a, "Achado");
            Assert.All(a.GetProperty("evidencias").EnumerateArray(), e => Conforme(e, "Evidencia"));
        });
    }

    [Fact]
    public async Task Envelope_rejeitado_segue_os_schemas_do_contrato()
    {
        var resposta = await ClienteExecucoes.CriarAsync(_ambiente.CriarCliente(), cancelamento: Cancelamento);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.Status);
        using var json = JsonDocument.Parse(resposta.Corpo);
        Conforme(json.RootElement, "EnvelopeV1");
        Assert.Equal("Rejeitada", json.RootElement.GetProperty("estado").GetString());
        Conforme(json.RootElement.GetProperty("erro"), "Erro");
    }

    [Fact]
    public async Task Execucao_inexistente_devolve_ErroResposta()
    {
        var resposta = await ClienteExecucoes.ObterAsync(_ambiente.CriarCliente(), Guid.CreateVersion7(), cancelamento: Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, resposta.Status);
        using var json = JsonDocument.Parse(resposta.Corpo);
        Conforme(json.RootElement, "ErroResposta");
        Conforme(json.RootElement.GetProperty("erro"), "Erro");
    }

    [Fact]
    public async Task Consulta_da_execucao_devolve_o_mesmo_envelope_da_criacao()
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        var cliente = _ambiente.CriarCliente();
        var criada = await ClienteExecucoes.CriarAsync(cliente, [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)], cancelamento: Cancelamento);

        var consultada = await ClienteExecucoes.ObterAsync(cliente, criada.Envelope.ExecucaoId, cancelamento: Cancelamento);

        Assert.Equal(HttpStatusCode.OK, consultada.Status);
        using var json = JsonDocument.Parse(consultada.Corpo);
        Conforme(json.RootElement, "EnvelopeV1");
        Assert.Equal(
            JsonSerializer.Serialize(criada.Envelope.Parecer, OpcoesJsonContrato.Padrao),
            JsonSerializer.Serialize(consultada.Envelope.Parecer, OpcoesJsonContrato.Padrao));
    }

    private void Conforme(JsonElement objeto, string esquema)
    {
        Assert.Equal(JsonValueKind.Object, objeto.ValueKind);
        var definicao = _esquemas[esquema];
        var nomes = objeto.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.All(definicao.Requeridos, requerido => Assert.True(nomes.Contains(requerido), $"{esquema}: '{requerido}' é obrigatório."));
        Assert.All(nomes, nome => Assert.True(definicao.Propriedades.Contains(nome), $"{esquema}: '{nome}' não está no contrato."));
    }
}
