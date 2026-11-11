using System.Text.Json;
using Api.Agente.A2A;
using Api.Agente.A2A.Sofia;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Tests.Integracao.Infra;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>TS-032 e mensagens inválidas no A2A (T142).</summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class MensagensA2ATests(AmbienteIntegracao ambiente)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("{\"texto\":true}")]
    [InlineData("nao-json")]
    [InlineData("{\"versaoContrato\":\"1.0\",\"idExecucao\":\"00000000-0000-0000-0000-000000000000\",\"idSolicitacao\":\"e1b3a4cb-d729-4bd4-b796-b9c94be07b46\",\"usuario\":{\"nome\":\"Maria\"},\"pergunta\":\"x\",\"documentos\":[{\"idDocumento\":\"d\",\"nome\":\"a.xml\",\"texto\":\"<n/>\"}]}")]
    public async Task Mensagem_fora_do_contrato_devolve_failed_sem_Artifact(string texto)
    {
        ambiente.ChatClient.Limpar();
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(ambiente.Fabrica);
        using var resposta = await SofiaEnvioA2A.EnviarTextoAsync(cliente, texto, Cancelamento);
        var corpo = await resposta.Content.ReadAsStringAsync(Cancelamento);
        Assert.Contains("failed", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ERR-007", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain(ContratoSofiaV1.NomeArtifact, corpo, StringComparison.Ordinal);
        Assert.Empty(ambiente.ChatClient.Chamadas);
    }

    [Fact]
    public async Task Violacao_de_limite_devolve_completed_com_status_erro()
    {
        ambiente.ChatClient.Limpar();
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(ambiente.Fabrica);
        var solicitacao = new SolicitacaoAnaliseSofia(
            ContratoSofiaV1.Versao,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new UsuarioSofia("Maria"),
            "Pergunta",
            []);
        using var resposta = await SofiaEnvioA2A.EnviarTextoAsync(
            cliente,
            JsonSerializer.Serialize(solicitacao, ContratoSofiaV1.Json),
            Cancelamento);
        var corpo = await resposta.Content.ReadAsStringAsync(Cancelamento);
        Assert.Contains("completed", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ContratoSofiaV1.NomeArtifact, corpo, StringComparison.Ordinal);
        Assert.Contains(CodigoErro.SolicitacaoSemConteudo, corpo, StringComparison.Ordinal);
        Assert.Empty(ambiente.ChatClient.Chamadas);
    }
}
