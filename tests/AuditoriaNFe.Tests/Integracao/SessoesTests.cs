using System.Net;
using System.Xml.Linq;
using AuditoriaNFe.Application.ContextProviders;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Parecer;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.Extensions.AI;
using MongoDB.Bson;
using MongoDB.Driver;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>
/// Continuidade por <c>sessaoId</c> (US4; FR-036 a FR-041): pergunta sem reenvio de arquivos, novos
/// documentos com duplicidade contra a sessão, isolamento por usuário e chamador, expiração e
/// encerramento.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class SessoesTests
{
    private readonly AmbienteIntegracao _ambiente;

    public SessoesTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pergunta_de_continuidade_usa_a_base_da_sessao_sem_reenvio_TS021()
    {
        const string Usuario = "usuario-sessao-021";
        const string Pergunta = "Qual o valor total da nota analisada?";
        _ambiente.ChatClient.Responder(RespostasIa.Valida()).Responder(RespostasIa.Valida());
        var cliente = _ambiente.CriarCliente();

        var analise = Concluida(await ClienteExecucoes.CriarAsync(cliente, [ArquivoEnvio.Fixture(NFeValida)], usuarioId: Usuario, cancelamento: Cancelamento));
        var sessaoId = analise.SessaoId!.Value;
        var continuidade = Concluida(await ClienteExecucoes.CriarAsync(cliente, pergunta: Pergunta, usuarioId: Usuario, sessaoId: sessaoId, cancelamento: Cancelamento));

        Assert.Equal(sessaoId, continuidade.SessaoId);
        Assert.Empty(continuidade.Parecer!.Documentos);
        Assert.DoesNotContain(LimitacoesParecer.SemBaseAnalisada, continuidade.Parecer.Limitacoes);
        var chamada = _ambiente.ChatClient.Chamadas[1];
        var contextoSessao = Assert.Single(chamada.Mensagens, m => m.Text.StartsWith(ContextoSessaoProvider.InicioContexto, StringComparison.Ordinal));
        Assert.Equal(ChatRole.User, contextoSessao.Role);
        Assert.Contains(Chave(NFeValida), contextoSessao.Text, StringComparison.Ordinal);
        Assert.Contains("\"semBaseAnalisada\":false", contextoSessao.Text, StringComparison.Ordinal);
        Assert.Contains(chamada.Mensagens, m => m.Role == ChatRole.Assistant);
        Assert.Contains(chamada.Mensagens, m => m.Role == ChatRole.User && m.Text.Contains(Pergunta, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Novos_arquivos_entram_na_base_com_duplicidade_contra_a_sessao_TS022()
    {
        const string Usuario = "usuario-sessao-022";
        _ambiente.ChatClient.Responder(RespostasIa.Valida()).Responder(RespostasIa.Valida());
        var cliente = _ambiente.CriarCliente();

        var primeira = Concluida(await ClienteExecucoes.CriarAsync(cliente, [ArquivoEnvio.Fixture(NFeValida)], usuarioId: Usuario, cancelamento: Cancelamento));
        var sessaoId = primeira.SessaoId!.Value;
        var segunda = Concluida(await ClienteExecucoes.CriarAsync(
            cliente,
            [ArquivoEnvio.Fixture(NFeValida), ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)],
            usuarioId: Usuario,
            sessaoId: sessaoId,
            cancelamento: Cancelamento));

        var nomeValida = Path.GetFileName(NFeValida);
        Assert.Equal("sessão", Assert.Single(segunda.Parecer!.Documentos, d => d.Arquivo == nomeValida).DuplicadoDe);
        Assert.Contains(segunda.Parecer.Achados, a => a.Arquivo == nomeValida && a.Descricao.StartsWith(CodigoErro.DocumentoDuplicado, StringComparison.Ordinal));
        AchadoDaRegra(segunda, "W04-10");

        var sessao = await BancoMongoTeste.Colecao(_ambiente.Mongo, "sessoes")
            .Find(new BsonDocument("_id", BancoMongoTeste.Guid(sessaoId)))
            .SingleAsync(Cancelamento);
        Assert.Equal(2, sessao["documentos"].AsBsonArray.Count);
        Assert.Equal(
            new[] { Chave(NFeValida), Chave(FixturesXml.Casos.TotalizadorDivergente) }.Order(StringComparer.Ordinal),
            sessao["hashesPorChave"].AsBsonDocument.Names.Order(StringComparer.Ordinal));
        Assert.Equal(2, sessao["execucoes"].AsBsonArray.Count);
        Assert.Equal(BsonType.String, sessao["historicoAgente"].BsonType);
    }

    [Theory]
    [InlineData("outro-usuario", ClienteExecucoes.ChamadorPadrao)]
    [InlineData("dono-sessao-023", "outro-chamador")]
    public async Task Sessao_de_outro_usuario_ou_chamador_responde_ERR005_sem_revelar_TS023_SC009(string usuario, string chamador)
    {
        const string Dono = "dono-sessao-023";
        _ambiente.ChatClient.Responder(RespostasIa.Valida()).Responder(RespostasIa.Valida());
        var cliente = _ambiente.CriarCliente();
        var sessaoId = Concluida(await ClienteExecucoes.CriarAsync(cliente, [ArquivoEnvio.Fixture(NFeValida)], usuarioId: Dono, cancelamento: Cancelamento)).SessaoId!.Value;

        var alheia = Rejeitada(
            await ClienteExecucoes.CriarAsync(cliente, pergunta: "Quais documentos foram analisados?", usuarioId: usuario, chamadorId: chamador, sessaoId: sessaoId, cancelamento: Cancelamento),
            HttpStatusCode.NotFound,
            CodigoErro.SessaoNaoEncontrada);
        var inexistente = Rejeitada(
            await ClienteExecucoes.CriarAsync(cliente, pergunta: "Quais documentos foram analisados?", usuarioId: usuario, chamadorId: chamador, sessaoId: Guid.CreateVersion7(), cancelamento: Cancelamento),
            HttpStatusCode.NotFound,
            CodigoErro.SessaoNaoEncontrada);
        var encerramento = await ClienteSessoes.EncerrarAsync(cliente, sessaoId, usuario, chamador, Cancelamento);

        Assert.Equal(inexistente.Erro!.Mensagem, alheia.Erro!.Mensagem);
        Assert.Null(alheia.SessaoId);
        Assert.Equal(HttpStatusCode.NotFound, encerramento.Status);
        Assert.Equal(CodigoErro.SessaoNaoEncontrada, encerramento.ErroResposta.Erro.Codigo);
        Assert.Single(_ambiente.ChatClient.Chamadas);

        var doDono = Concluida(await ClienteExecucoes.CriarAsync(cliente, pergunta: "Quais documentos foram analisados?", usuarioId: Dono, sessaoId: sessaoId, cancelamento: Cancelamento));
        Assert.Equal(sessaoId, doDono.SessaoId);
    }

    [Fact]
    public async Task Sessao_expirada_responde_ERR006_TS024()
    {
        const string Usuario = "usuario-sessao-024";
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        var cliente = _ambiente.CriarCliente();
        var inicio = _ambiente.Relogio.Agora;
        var sessaoId = Concluida(await ClienteExecucoes.CriarAsync(cliente, [ArquivoEnvio.Fixture(NFeValida)], usuarioId: Usuario, cancelamento: Cancelamento)).SessaoId!.Value;

        try
        {
            _ambiente.Relogio.Avancar(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));
            var envelope = Rejeitada(
                await ClienteExecucoes.CriarAsync(cliente, pergunta: "E agora?", usuarioId: Usuario, sessaoId: sessaoId, cancelamento: Cancelamento),
                HttpStatusCode.Gone,
                CodigoErro.SessaoExpirada);

            Assert.Equal(sessaoId, envelope.SessaoId);
            Assert.Equal(MensagensErro.Para(CodigoErro.SessaoExpirada), envelope.Erro!.Mensagem);
            var sessao = await BancoMongoTeste.Colecao(_ambiente.Mongo, "sessoes")
                .Find(new BsonDocument("_id", BancoMongoTeste.Guid(sessaoId)))
                .SingleAsync(Cancelamento);
            Assert.Equal("Expirada", sessao["estado"].AsString);
            Assert.Single(_ambiente.ChatClient.Chamadas);
        }
        finally
        {
            _ambiente.Relogio.Definir(inicio);
        }
    }

    [Fact]
    public async Task Sessao_encerrada_fica_indisponivel_TS025()
    {
        const string Usuario = "usuario-sessao-025";
        _ambiente.ChatClient.Responder(RespostasIa.Valida());
        var cliente = _ambiente.CriarCliente();
        var sessaoId = Concluida(await ClienteExecucoes.CriarAsync(cliente, [ArquivoEnvio.Fixture(NFeValida)], usuarioId: Usuario, cancelamento: Cancelamento)).SessaoId!.Value;

        var encerramento = await ClienteSessoes.EncerrarAsync(cliente, sessaoId, Usuario, cancelamento: Cancelamento);
        var envelope = Rejeitada(
            await ClienteExecucoes.CriarAsync(cliente, pergunta: "Ainda posso perguntar?", usuarioId: Usuario, sessaoId: sessaoId, cancelamento: Cancelamento),
            HttpStatusCode.Gone,
            CodigoErro.SessaoExpirada);
        var repeticao = await ClienteSessoes.EncerrarAsync(cliente, sessaoId, Usuario, cancelamento: Cancelamento);

        Assert.Equal(HttpStatusCode.NoContent, encerramento.Status);
        Assert.Equal(MensagensErro.SessaoEncerrada, envelope.Erro!.Mensagem);
        Assert.Equal(HttpStatusCode.NoContent, repeticao.Status);
        Assert.Single(_ambiente.ChatClient.Chamadas);
    }

    private static string Chave(string caminho)
    {
        var conteudo = FixturesXml.Todas().Single(f => f.Caminho == caminho).Conteudo;
        var id = (string)XDocument.Load(new MemoryStream(conteudo)).Descendants(GeradorNFeSintetica.Nfe + "infNFe").Single().Attribute("Id")!;
        return id["NFe".Length..];
    }
}
