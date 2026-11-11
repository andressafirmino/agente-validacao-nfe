using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.ContextProviders;
using AuditoriaNFe.Application.Ferramentas;
using AuditoriaNFe.Application.Middleware;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Application.Agente;

/// <summary>
/// Monta o agente interno da etapa 7 (research R-05, constitution 3.3): <c>ChatClientAgent</c> com id
/// estável, as instruções versionadas, os provedores de contexto da execução, da sessão e do usuário
/// e o <see cref="MiddlewareExecucaoAgente"/>. A saída é pedida por <c>RunAsync&lt;ContratoIaV1&gt;</c>.
/// O histórico da sessão guarda só as mensagens externas e as respostas, reduzido às últimas
/// <c>Historico:MaxMensagens</c> a cada mensagem acrescentada (FR-042; research R-08).
/// </summary>
public sealed class FabricaAgenteInterno(
    IChatClient chatClient,
    MiddlewareExecucaoAgente middlewareExecucao,
    MiddlewareFunctionCalling middlewareFerramentas,
    FerramentasLeitura ferramentas,
    IOptions<HistoricoOpcoes> historico,
    ILoggerFactory logs)
{
    public const string IdAgente = "auditoria-nfe-agente-interno";
    public const string NomeAgente = "AuditoriaNFe";

    public AIAgent Criar(ContextoAgenteIa contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        List<AIContextProvider> provedores =
        [
            new ContextoExecucaoProvider(contexto.Execucao),
            new ContextoSessaoProvider(contexto.Sessao),
        ];
        if (contexto.Usuario is not null)
        {
            provedores.Add(new ContextoUsuarioProvider(contexto.Usuario));
        }

        var funcoes = ferramentas.CriarFuncoes()
            .Where(f => PoliticaFerramentas.Autorizada(contexto.Tipo, f.Name))
            .Cast<AITool>()
            .ToList();

        var agente = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Id = IdAgente,
                Name = NomeAgente,
                Description = "Agente interno de auditoria de NF-e modelo 55.",
                ChatOptions = new ChatOptions { Instructions = InstrucoesAgente.Texto, Tools = funcoes },
                AIContextProviders = provedores,
                ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
                {
#pragma warning disable MEAI001 // Redutor experimental no Microsoft.Extensions.AI; é o ponto de redução previsto em research R-08.
                    ChatReducer = new MessageCountingChatReducer(historico.Value.MaxMensagens),
#pragma warning restore MEAI001
                    ReducerTriggerEvent = InMemoryChatHistoryProviderOptions.ChatReducerTriggerEvent.AfterMessageAdded,
                    StorageInputRequestMessageFilter = mensagens =>
                        mensagens.Where(m => m.GetAgentRequestMessageSourceType() == AgentRequestMessageSourceType.External),
                }),
            },
            logs);
        return middlewareFerramentas.Aplicar(middlewareExecucao.Aplicar(agente));
    }
}
