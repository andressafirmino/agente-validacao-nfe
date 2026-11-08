using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Infrastructure.Persistencia.Mongo;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>
/// NFR-008 e P16: só com o <c>execucaoId</c>, o que foi gravado reconstrói a execução. O documento
/// <c>execucoes</c> do MongoDB (data-model §2, com os <c>arquivos</c> recebidos embutidos, §3) e as
/// linhas de <c>consumo.chamadas_provedor</c> são lidos sem passar pela API.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class RastreabilidadeExecucaoTests
{
    private static readonly string[] EstadosEtapa = ["Executada", "Falhou", "NaoAplicavel", "NaoExecutada"];
    private static readonly string[] ResultadosRegra = ["Conforme", "Divergente", "NaoAplicavel"];

    private readonly AmbienteIntegracao _ambiente;

    public RastreabilidadeExecucaoTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Execucao_com_manuais_e_IA_e_reconstruida_a_partir_do_execucaoId()
    {
        var consulta = new ConsultaManuaisFalsa([ConsultaManuaisFalsa.Trecho()]);
        var fabrica = _ambiente.CriarFabrica(
            configuracoes: new Dictionary<string, string?> { ["Mcp:Habilitado"] = "true", ["Mcp:Endpoint"] = "http://127.0.0.1:9/mcp" },
            servicos: s =>
            {
                s.RemoveAll<IConsultaManuais>();
                s.AddSingleton<IConsultaManuais>(consulta);
            });
        _ambiente.ChatClient.ResponderComUso(RespostasIa.Valida(), tokensEntrada: 1200, tokensSaida: 300);

        var execucaoId = Concluida(await ClienteExecucoes.CriarAsync(
            fabrica.CreateClient(),
            [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente), ArquivoEnvio.Fixture(NFeValida)],
            cancelamento: Cancelamento)).ExecucaoId;

        var execucao = await ExecucaoGravadaAsync(execucaoId);

        EtapasCompletas(execucao);
        var arquivos = execucao["arquivos"].AsBsonArray.Select(a => a.AsBsonDocument).ToList();
        Assert.Equal(2, arquivos.Count);
        Assert.All(arquivos, arquivo =>
        {
            var layout = arquivo["validacaoLayout"].AsBsonDocument;
            Assert.Equal(FixturesXml.Casos.Pacote, layout["pacoteAplicado"].AsString);
            Assert.Equal(new DateOnly(2026, 11, 2), Data(layout["dataReferencia"]));
            var regras = arquivo["regrasAplicadas"].AsBsonArray.Select(r => r.AsBsonDocument).ToList();
            Assert.NotEmpty(regras);
            Assert.All(regras, regra =>
            {
                Assert.False(string.IsNullOrWhiteSpace(regra["regraId"].AsString));
                Assert.True(regra["versaoRegra"].AsInt32 >= 1);
                Assert.Contains(regra["resultado"].AsString, ResultadosRegra);
            });
        });

        var divergente = arquivos.Single(a => a["nome"].AsString == "totalizador-divergente.xml")["regrasAplicadas"].AsBsonArray
            .Select(r => r.AsBsonDocument).Single(r => r["regraId"].AsString == "W04-10");
        Assert.Equal((1, "Divergente"), (divergente["versaoRegra"].AsInt32, divergente["resultado"].AsString));
        Assert.DoesNotContain(arquivos.Single(a => a["nome"].AsString == Path.GetFileName(NFeValida))["regrasAplicadas"].AsBsonArray,
            r => r["resultado"].AsString == "Divergente");

        var trecho = Assert.Single(execucao["trechosConsultados"].AsBsonArray).AsBsonDocument;
        Assert.Equal(("manual-sintetico-1.0-001", "MANUAL-SINTETICO", "1.0"), (trecho["chunkId"].AsString, trecho["documento"].AsString, trecho["versao"].AsString));

        var linhas = await LinhasConsumoAsync(_ambiente.Postgres, execucaoId, Cancelamento);
        var linha = Assert.Single(linhas);
        Assert.Equal<(int?, int?, int)>((1200, 300, 1), (linha.TokensEntrada, linha.TokensSaida, linha.Tentativa));
    }

    [Fact]
    public async Task Execucao_sem_MCP_registra_etapa_5_nao_aplicavel_e_nenhum_trecho()
    {
        _ambiente.ChatClient.Responder(RespostasIa.Valida());

        var execucaoId = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(), [ArquivoEnvio.Fixture(NFeValida)], cancelamento: Cancelamento)).ExecucaoId;

        var execucao = await ExecucaoGravadaAsync(execucaoId);
        EtapasCompletas(execucao);
        var etapa5 = execucao["etapas"].AsBsonArray.Select(e => e.AsBsonDocument).Single(e => e["numero"].AsInt32 == 5);
        Assert.Equal("NaoAplicavel", etapa5["estado"].AsString);
        Assert.Empty(execucao["trechosConsultados"].AsBsonArray);
        Assert.False(execucao["integracaoMcpHabilitada"].AsBoolean);
    }

    private async Task<BsonDocument> ExecucaoGravadaAsync(Guid execucaoId)
    {
        var url = MongoUrl.Create(_ambiente.Mongo.ConnectionString);
        var banco = new MongoClient(url).GetDatabase(url.DatabaseName ?? ContextoMongo.BancoPadrao);
        var filtro = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(execucaoId, GuidRepresentation.Standard));
        return Assert.Single(await banco.GetCollection<BsonDocument>("execucoes").Find(filtro).ToListAsync(Cancelamento));
    }

    private static void EtapasCompletas(BsonDocument execucao)
    {
        var etapas = execucao["etapas"].AsBsonArray.Select(e => e.AsBsonDocument).ToList();
        Assert.Equal(Enumerable.Range(1, 9), etapas.Select(e => e["numero"].AsInt32));
        Assert.All(etapas, etapa =>
        {
            Assert.Contains(etapa["estado"].AsString, EstadosEtapa);
            if (etapa["estado"].AsString is "Executada" or "Falhou")
            {
                var inicio = etapa["inicioEm"].ToUniversalTime();
                var fim = etapa["fimEm"].ToUniversalTime();
                Assert.True(fim >= inicio, $"Etapa {etapa["numero"]}: fim antes do início.");
            }
        });
    }

    private static DateOnly Data(BsonValue valor) => valor.BsonType switch
    {
        BsonType.DateTime => DateOnly.FromDateTime(valor.ToUniversalTime()),
        BsonType.String => DateOnly.Parse(valor.AsString, System.Globalization.CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"Data de referência em formato inesperado: {valor.BsonType}."),
    };
}
