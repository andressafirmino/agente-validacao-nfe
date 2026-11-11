using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using A2A.V0_3;
using Api.Agente.A2A;
using Api.Agente.A2A.Sofia;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Tests.Contrato;
using AuditoriaNFe.Tests.Duplos;
using AuditoriaNFe.Tests.Fixtures.Xml;
using AuditoriaNFe.Tests.Integracao.Infra;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>Cenários TS-030 a TS-069 pelo canal A2A (T143).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class A2AFimAFimTests(AmbienteIntegracao ambiente)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TS030_achados_deterministicos_via_A2A()
    {
        ambiente.ChatClient.Responder(RespostasIa.Valida());
        var idExec = Guid.CreateVersion7();
        var idSol = Guid.CreateVersion7();
        var xml = Encoding.UTF8.GetString(ArquivoEnvio.Fixture(FixturesXml.Casos.TotalizadorDivergente).Conteudo);
        var solicitacao = CriarSolicitacao(idExec, idSol, "Ana", xml);

        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(ambiente.Fabrica);
        var corpo = await SofiaEnvioA2A.EnviarSolicitacaoAsync(cliente, solicitacao, Cancelamento);
        var resultado = ExtrairResultado(corpo);

        Assert.Equal("concluido", resultado.Status);
        Assert.Equal(idExec, resultado.IdExecucao);
        Assert.Equal(idSol, resultado.IdSolicitacao);
        Assert.NotEmpty(resultado.Achados);
    }

    [Fact]
    public async Task TS034_ids_SOFIA_preservados_na_resposta()
    {
        ambiente.ChatClient.Responder(RespostasIa.Valida());
        var idExec = Guid.Parse("8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e");
        var idSol = Guid.Parse("e1b3a4cb-d729-4bd4-b796-b9c94be07b46");
        var xml = TextoNFeValida();
        var solicitacao = CriarSolicitacao(idExec, idSol, "Carlos", xml);
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(ambiente.Fabrica);
        var corpo = await SofiaEnvioA2A.EnviarSolicitacaoAsync(cliente, solicitacao, Cancelamento);
        var resultado = ExtrairResultado(corpo);
        Assert.Equal(idExec, resultado.IdExecucao);
        Assert.Equal(idSol, resultado.IdSolicitacao);
    }

    [Fact]
    public async Task TS065_arquivo_nao_nfe_devolve_parcial()
    {
        ambiente.ChatClient.Responder(RespostasIa.Valida());
        var solicitacao = CriarSolicitacao(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Lucia",
            TextoNFeValida(),
            segundoTexto: "não é xml");
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(ambiente.Fabrica);
        var corpo = await SofiaEnvioA2A.EnviarSolicitacaoAsync(cliente, solicitacao, Cancelamento);
        var resultado = ExtrairResultado(corpo);
        Assert.Equal("parcial", resultado.Status);
    }

    [Fact]
    public async Task TS068_repeticao_idSolicitacao_concluida_nao_chama_novamente_a_IA()
    {
        ambiente.ChatClient.Limpar().Responder(RespostasIa.Valida());
        var idExec = Guid.CreateVersion7();
        var idSol = Guid.CreateVersion7();
        var xml = TextoNFeValida();
        var solicitacao = CriarSolicitacao(idExec, idSol, "Pedro", xml);
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(ambiente.Fabrica);

        await SofiaEnvioA2A.EnviarSolicitacaoAsync(cliente, solicitacao, Cancelamento);
        Assert.Single(ambiente.ChatClient.Chamadas);

        var corpo = await SofiaEnvioA2A.EnviarSolicitacaoAsync(cliente, solicitacao, Cancelamento);
        var resultado = ExtrairResultado(corpo);
        Assert.Single(ambiente.ChatClient.Chamadas);
        Assert.Equal("concluido", resultado.Status);
        Assert.Equal(idExec, resultado.IdExecucao);
    }

    [Fact]
    public async Task Cliente_A2A_V03_resolve_card_e_envia_mensagem()
    {
        ambiente.ChatClient.Responder(RespostasIa.Valida());
        using var http = ambiente.Fabrica.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CredencialServicoSofiaTests.Chave);
        var baseUri = http.BaseAddress ?? new Uri("http://localhost");
        var resolver = new A2ACardResolver(baseUri, http);
        var card = await resolver.GetAgentCardAsync(Cancelamento);
        Assert.Contains("/a2a/auditoria-nfe", card.Url, StringComparison.Ordinal);

        var clienteA2A = new A2AClient(new Uri(card.Url), http);
        var idExec = Guid.CreateVersion7();
        var idSol = Guid.CreateVersion7();
        var xml = TextoNFeValida();
        var texto = JsonSerializer.Serialize(CriarSolicitacao(idExec, idSol, "Rita", xml), ContratoSofiaV1.Json);
        var resposta = await clienteA2A.SendMessageAsync(
            new MessageSendParams
            {
                Message = new AgentMessage
                {
                    Role = MessageRole.User,
                    Parts = [new TextPart { Text = texto }],
                },
            },
            Cancelamento);
        var tarefa = Assert.IsType<AgentTask>(resposta);
        Assert.Equal(TaskState.Completed, tarefa.Status.State);
        Assert.NotNull(tarefa.Artifacts);
        Assert.Contains(tarefa.Artifacts, a => a.Name == ContratoSofiaV1.NomeArtifact);
    }

    private static SolicitacaoAnaliseSofia CriarSolicitacao(
        Guid idExec,
        Guid idSol,
        string nome,
        string xmlNfe,
        string? segundoTexto = null)
    {
        var docs = new List<DocumentoSofia>
        {
            new("doc-nfe", "nfe.xml", xmlNfe),
        };
        if (segundoTexto is not null)
        {
            docs.Add(new DocumentoSofia("doc-2", "lixo.txt", segundoTexto));
        }

        return new SolicitacaoAnaliseSofia(
            ContratoSofiaV1.Versao,
            idExec,
            idSol,
            new UsuarioSofia(nome),
            "Verifique divergências nos documentos.",
            docs);
    }

    private static string TextoNFeValida() =>
        Encoding.UTF8.GetString(ArquivoEnvio.Fixture(NFeValida).Conteudo);

    private static ResultadoAnaliseSofia ExtrairResultado(string corpoRpc)
    {
        using var documento = JsonDocument.Parse(corpoRpc);
        var artifacts = documento.RootElement.GetProperty("result").GetProperty("artifacts");
        string? texto = null;
        foreach (var artifact in artifacts.EnumerateArray())
        {
            if (artifact.GetProperty("name").GetString() != ContratoSofiaV1.NomeArtifact)
            {
                continue;
            }

            texto = artifact.GetProperty("parts")[0].GetProperty("text").GetString();
            break;
        }

        if (texto is null)
        {
            throw new InvalidOperationException("Artifact SOFIA ausente na resposta A2A.");
        }
        return JsonSerializer.Deserialize<ResultadoAnaliseSofia>(texto, ContratoSofiaV1.Json)
            ?? throw new InvalidOperationException("Resultado inválido.");
    }
}
