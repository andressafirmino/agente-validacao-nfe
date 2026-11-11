using System.Text.Json;
using A2A.V0_3;
using Sofia.Simulator.Contrato;
using Sofia.Simulator.Exibicao;

namespace Sofia.Simulator.Cenarios;

internal static class EnvioSofia
{
    public static async Task<(AgentTask Tarefa, ResultadoAnaliseSofia Resultado)> EnviarAsync(
        A2AClient cliente,
        SolicitacaoAnaliseSofia solicitacao,
        TextWriter saida,
        CancellationToken cancellationToken)
    {
        ExibicaoConsole.ExibirSolicitacaoEnviada(saida, solicitacao.IdExecucao, solicitacao.IdSolicitacao);
        var texto = JsonSerializer.Serialize(solicitacao, ContratoSofiaV1.Json);
        var resposta = await cliente.SendMessageAsync(
            new MessageSendParams
            {
                Message = new AgentMessage
                {
                    Role = MessageRole.User,
                    Parts = [new TextPart { Text = texto }],
                },
            },
            cancellationToken);

        var tarefa = resposta as AgentTask
            ?? throw new InvalidOperationException("Resposta A2A inesperada: esperava uma Task.");
        var resultado = ExtrairResultado(tarefa);
        return (tarefa, resultado);
    }

    private static ResultadoAnaliseSofia ExtrairResultado(AgentTask tarefa)
    {
        if (tarefa.Artifacts is null || tarefa.Artifacts.Count == 0)
        {
            throw new InvalidOperationException("Artifact analise-sofia-v1 ausente na Task.");
        }

        string? json = null;
        using var documento = JsonSerializer.SerializeToDocument(tarefa);
        if (documento.RootElement.TryGetProperty("artifacts", out var artifacts))
        {
            foreach (var artifact in artifacts.EnumerateArray())
            {
                if (artifact.GetProperty("name").GetString() != ContratoSofiaV1.NomeArtifact)
                {
                    continue;
                }

                foreach (var part in artifact.GetProperty("parts").EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var texto))
                    {
                        json = texto.GetString();
                        break;
                    }
                }

                break;
            }
        }

        if (json is null)
        {
            throw new InvalidOperationException("Artifact analise-sofia-v1 sem parte de texto.");
        }

        return JsonSerializer.Deserialize<ResultadoAnaliseSofia>(json, ContratoSofiaV1.Json)
            ?? throw new InvalidOperationException("Resultado SOFIA inválido.");
    }
}
