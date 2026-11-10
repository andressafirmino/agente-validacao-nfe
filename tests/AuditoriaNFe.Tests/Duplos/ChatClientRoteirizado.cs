using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AuditoriaNFe.Application.Contratos;
using Microsoft.Extensions.AI;

namespace AuditoriaNFe.Tests.Duplos;

/// <summary>Uma chamada recebida pelo <see cref="ChatClientRoteirizado"/>.</summary>
public sealed record ChamadaRecebida(IReadOnlyList<ChatMessage> Mensagens, ChatOptions? Opcoes);

/// <summary>
/// <see cref="IChatClient"/> que devolve, em ordem, as respostas roteirizadas pelo teste: válidas,
/// inválidas, exceção ou tempo esgotado, com ou sem <see cref="UsageDetails"/> (research R-23).
/// Chamada além do roteiro falha, para que nenhum teste dependa de resposta implícita.
/// </summary>
public sealed class ChatClientRoteirizado(string modelo = ChatClientRoteirizado.ModeloPadrao) : IChatClient
{
    public const string ModeloPadrao = "gpt-5-mini";

    private readonly ConcurrentQueue<Func<CancellationToken, Task<ChatResponse>>> _roteiro = new();
    private readonly ConcurrentQueue<ChamadaRecebida> _chamadas = new();

    public IReadOnlyList<ChamadaRecebida> Chamadas => [.. _chamadas];

    public int Restantes => _roteiro.Count;

    /// <summary>Descarta o roteiro e as chamadas de testes anteriores que usam a mesma instância.</summary>
    public ChatClientRoteirizado Limpar()
    {
        _roteiro.Clear();
        _chamadas.Clear();
        return this;
    }

    /// <summary>
    /// Resposta com o texto dado (válido ou não para o contrato) e o uso informado ou ausente;
    /// <paramref name="modeloResposta"/> substitui o modelo padrão nesta resposta.
    /// </summary>
    public ChatClientRoteirizado Responder(string texto, UsageDetails? uso = null, string? modeloResposta = null)
    {
        _roteiro.Enqueue(_ => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, texto))
        {
            ModelId = modeloResposta ?? modelo,
            FinishReason = ChatFinishReason.Stop,
            Usage = uso,
        }));
        return this;
    }

    public ChatClientRoteirizado ResponderJson<T>(T conteudo, UsageDetails? uso = null) =>
        Responder(JsonSerializer.Serialize(conteudo, OpcoesJsonContrato.Padrao), uso);

    public ChatClientRoteirizado ResponderComUso(string texto, long tokensEntrada, long tokensSaida, long? tokensEntradaCache = null) =>
        Responder(texto, new UsageDetails
        {
            InputTokenCount = tokensEntrada,
            OutputTokenCount = tokensSaida,
            CachedInputTokenCount = tokensEntradaCache,
            TotalTokenCount = tokensEntrada + tokensSaida,
        });

    public ChatClientRoteirizado Lancar(Exception erro)
    {
        ArgumentNullException.ThrowIfNull(erro);
        _roteiro.Enqueue(_ => Task.FromException<ChatResponse>(erro));
        return this;
    }

    /// <summary>Não responde até o cancelamento, como um provedor que estoura o timeout.</summary>
    public ChatClientRoteirizado EsgotarTempo()
    {
        _roteiro.Enqueue(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Inalcançável.");
        });
        return this;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _chamadas.Enqueue(new ChamadaRecebida([.. messages], options?.Clone()));
        return _roteiro.TryDequeue(out var passo)
            ? passo(cancellationToken)
            : Task.FromException<ChatResponse>(new InvalidOperationException("Roteiro do ChatClientRoteirizado esgotado."));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var resposta = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var atualizacao in resposta.ToChatResponseUpdates())
        {
            yield return atualizacao;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is not null ? null
        : serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("Roteirizado", defaultModelId: modelo)
        : serviceType.IsInstanceOfType(this) ? this
        : null;

    public void Dispose()
    {
    }
}
