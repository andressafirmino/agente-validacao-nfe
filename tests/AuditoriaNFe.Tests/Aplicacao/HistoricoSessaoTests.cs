using System.Xml.Linq;
using AuditoriaNFe.Application.ContextProviders;
using AuditoriaNFe.Domain.Parecer;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.Extensions.AI;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Aplicacao;

/// <summary>
/// Histórico da sessão do agente (FR-037, FR-042; research R-08): a redução por
/// <c>Historico:MaxMensagens</c> tira mensagens antigas da conversa, mas os resultados das análises
/// continuam disponíveis pelo contexto da sessão; pergunta sem base analisada é informada.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class HistoricoSessaoTests
{
    private readonly AmbienteIntegracao _ambiente;

    public HistoricoSessaoTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Historico_reduzido_mantem_os_resultados_das_analises_FR042()
    {
        const string Usuario = "usuario-historico";
        string[] perguntas = ["Primeira pergunta sobre o lote?", "Segunda pergunta sobre o lote?", "Terceira pergunta sobre o lote?"];
        _ambiente.ChatClient
            .Responder(RespostasIa.Valida())
            .Responder(RespostasIa.RespostaPerguntaSimples("Continuidade respondida."))
            .Responder(RespostasIa.RespostaPerguntaSimples("Continuidade respondida."))
            .Responder(RespostasIa.RespostaPerguntaSimples("Continuidade respondida."));

        var cliente = _ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?> { ["Historico:MaxMensagens"] = "2" }).CreateClient();
        var sessaoId = Concluida(await ClienteExecucoes.CriarAsync(
            cliente, [ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente)], usuarioId: Usuario, cancelamento: Cancelamento)).SessaoId;
        foreach (var pergunta in perguntas)
        {
            Concluida(await ClienteExecucoes.CriarAsync(cliente, pergunta: pergunta, usuarioId: Usuario, sessaoId: sessaoId, cancelamento: Cancelamento));
        }

        var ultima = _ambiente.ChatClient.Chamadas[^1].Mensagens;
        var conversa = ultima.Where(m => !m.Text.StartsWith("<<<CONTEXTO_", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, conversa.Count);
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.User], conversa.Select(m => m.Role));
        Assert.DoesNotContain(ultima, m => m.Text.Contains(perguntas[0], StringComparison.Ordinal));
        Assert.Contains(perguntas[1], conversa[0].Text, StringComparison.Ordinal);
        Assert.Contains(perguntas[2], conversa[2].Text, StringComparison.Ordinal);

        var contextoSessao = Assert.Single(ultima, m => m.Text.StartsWith(ContextoSessaoProvider.InicioContexto, StringComparison.Ordinal)).Text;
        Assert.Contains(Chave(FixturesXml.Casos.TotalizadorDivergente), contextoSessao, StringComparison.Ordinal);
        Assert.Contains("\"regraId\":\"W04-10\"", contextoSessao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pergunta_em_sessao_sem_analise_informa_que_nao_ha_base_analisada()
    {
        _ambiente.ChatClient.Responder(RespostasIa.ComIndisponibilidade(
            "Não há notas analisadas nesta sessão; envie os XMLs para formar a base antes de perguntar sobre valores."));

        var envelope = Concluida(await ClienteExecucoes.CriarAsync(
            _ambiente.CriarCliente(), pergunta: "Qual é o total do ICMS das notas?", usuarioId: "usuario-sem-base", cancelamento: Cancelamento));

        Assert.Contains(LimitacoesParecer.SemBaseAnalisada, envelope.Parecer!.Limitacoes);
        var contextoSessao = Assert.Single(
            _ambiente.ChatClient.Chamadas.Single().Mensagens,
            m => m.Text.StartsWith(ContextoSessaoProvider.InicioContexto, StringComparison.Ordinal)).Text;
        Assert.Contains("\"semBaseAnalisada\":true", contextoSessao, StringComparison.Ordinal);
        Assert.DoesNotContain("\"observacao\":null", contextoSessao, StringComparison.Ordinal);
    }

    private static string Chave(string caminho)
    {
        var conteudo = FixturesXml.Todas().Single(f => f.Caminho == caminho).Conteudo;
        var id = (string)XDocument.Load(new MemoryStream(conteudo)).Descendants(GeradorNFeSintetica.Nfe + "infNFe").Single().Attribute("Id")!;
        return id["NFe".Length..];
    }
}
