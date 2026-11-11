using System.Text.Json;
using A2A.V0_3;
using Sofia.Simulator.Contrato;

namespace Sofia.Simulator.Exibicao;

public static class ExibicaoConsole
{
    public static void ExibirCard(TextWriter saida, AgentCard card)
    {
        saida.WriteLine("=== Agent Card (A2A v0.3) ===");
        saida.WriteLine($"Nome: {card.Name}");
        saida.WriteLine($"URL do endpoint A2A: {card.Url}");
        saida.WriteLine($"Protocolo: {card.ProtocolVersion}");
        if (card.Skills is { Count: > 0 } habilidades)
        {
            saida.WriteLine("Habilidades:");
            foreach (var habilidade in habilidades)
            {
                saida.WriteLine($"  - {habilidade.Id}: {habilidade.Name}");
            }
        }

        if (card.SecuritySchemes is { Count: > 0 })
        {
            saida.WriteLine("Autenticação (esquemas declarados no card):");
            foreach (var (nome, _) in card.SecuritySchemes)
            {
                saida.WriteLine($"  - {nome}: Bearer");
            }
        }

        var json = JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
        saida.WriteLine("Limites e extensão SOFIA (trecho do card):");
        using (var documento = JsonDocument.Parse(json))
        {
            if (documento.RootElement.TryGetProperty("capabilities", out var capabilities)
                && capabilities.TryGetProperty("extensions", out var extensions))
            {
                foreach (var extensao in extensions.EnumerateArray())
                {
                    if (!extensao.TryGetProperty("params", out var parametros))
                    {
                        continue;
                    }

                    if (parametros.TryGetProperty("idAgente", out var idAgente))
                    {
                        saida.WriteLine($"  idAgente: {idAgente.GetString()}");
                    }

                    if (parametros.TryGetProperty("limites", out var limites))
                    {
                        if (limites.TryGetProperty("quantidadeDocumentos", out var qtd))
                        {
                            saida.WriteLine($"  limite quantidade de documentos: {qtd}");
                        }

                        if (limites.TryGetProperty("tamanhoTotalBytes", out var bytes))
                        {
                            saida.WriteLine($"  limite tamanho total (bytes): {bytes}");
                        }
                    }
                }
            }
        }

        saida.WriteLine();
    }

    public static void ExibirSolicitacaoEnviada(TextWriter saida, Guid idExecucao, Guid idSolicitacao)
    {
        saida.WriteLine("=== Solicitação enviada (message/send) ===");
        saida.WriteLine($"idExecucao: {idExecucao:D}");
        saida.WriteLine($"idSolicitacao: {idSolicitacao:D}");
        saida.WriteLine();
    }

    public static void ExibirTarefa(TextWriter saida, AgentTask tarefa, ResultadoAnaliseSofia resultado, Guid idExecucao, Guid idSolicitacao)
    {
        saida.WriteLine("=== Resposta A2A ===");
        saida.WriteLine($"Estado da Task: {tarefa.Status.State}");
        saida.WriteLine($"Status SOFIA: {resultado.Status}");
        saida.WriteLine($"Análise: {resultado.Analise}");
        if (resultado.Achados.Count > 0)
        {
            saida.WriteLine("Achados:");
            foreach (var achado in resultado.Achados)
            {
                var severidade = achado.Severidade is { } s ? $" [{s}]" : string.Empty;
                saida.WriteLine($"  - {achado.IdAchado}{severidade}: {achado.Descricao}");
            }
        }

        if (resultado.Observacoes is { Count: > 0 } observacoes)
        {
            saida.WriteLine("Observações:");
            foreach (var observacao in observacoes)
            {
                saida.WriteLine($"  - {observacao}");
            }
        }

        if (resultado.Erro is { } erro)
        {
            saida.WriteLine($"Erro: {erro.Codigo} — {erro.Mensagem}");
        }

        var idsOk = resultado.IdExecucao == idExecucao && resultado.IdSolicitacao == idSolicitacao;
        saida.WriteLine();
        saida.WriteLine(idsOk
            ? "Conferência dos IDs: os valores devolvidos coincidem com os enviados."
            : "Conferência dos IDs: FALHA — valores diferentes dos enviados.");
        saida.WriteLine();
    }
}
