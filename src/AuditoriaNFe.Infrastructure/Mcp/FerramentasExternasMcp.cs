using System.Text.Json;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AuditoriaNFe.Infrastructure.Mcp;

/// <summary>
/// Consulta <c>listar_capacidades_externas_v1</c> na Api.RAG (preparação da etapa 6).
/// </summary>
public sealed partial class FerramentasExternasMcp(IOptions<McpOpcoes> opcoes, ILoggerFactory logs) : IFerramentasExternas
{
    public const string NomeFerramenta = "listar_capacidades_externas_v1";

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);
    private readonly ILogger _logger = logs.CreateLogger<FerramentasExternasMcp>();

    public bool Habilitada => true;

    public async Task<ResultadoFerramentasExternas> ConsultarCapacidadesAsync(CancellationToken cancellationToken)
    {
        var configuracao = opcoes.Value;
        if (!Uri.TryCreate(configuracao.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new FerramentasExternasIndisponivelException("Mcp:Endpoint ausente ou inválido.");
        }

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(configuracao.Timeout);

        var transporte = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = endpoint,
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = configuracao.Timeout,
            },
            logs);
        await using var cliente = await McpClient.CreateAsync(transporte, loggerFactory: logs, cancellationToken: limite.Token)
            .ConfigureAwait(false);

        var ferramentas = await cliente.ListToolsAsync(cancellationToken: limite.Token).ConfigureAwait(false);
        if (!ferramentas.Any(f => f.Name == NomeFerramenta))
        {
            LogFerramentaAusente(_logger, NomeFerramenta);
            return new ResultadoFerramentasExternas([]);
        }

        var resultado = await cliente.CallToolAsync(
            NomeFerramenta,
            new Dictionary<string, object?>(StringComparer.Ordinal),
            cancellationToken: limite.Token).ConfigureAwait(false);
        if (resultado.IsError == true)
        {
            throw new FerramentasExternasIndisponivelException("O servidor MCP devolveu erro na ferramenta de capacidades externas.");
        }

        return Interpretar(resultado);
    }

    private static ResultadoFerramentasExternas Interpretar(CallToolResult resultado)
    {
        var json = resultado.StructuredContent is { } estruturado
            ? JsonSerializer.Serialize(estruturado, OpcoesJson)
            : resultado.Content.OfType<TextContentBlock>().Select(t => t.Text).FirstOrDefault()
                ?? throw new FerramentasExternasIndisponivelException("Resposta MCP sem conteúdo.");

        RespostaJson? resposta;
        try
        {
            resposta = JsonSerializer.Deserialize<RespostaJson>(json, OpcoesJson);
        }
        catch (JsonException erro)
        {
            throw new FerramentasExternasIndisponivelException("Resposta MCP fora do contrato.", erro);
        }

        var capacidades = resposta?.Capacidades?.Where(c => !string.IsNullOrWhiteSpace(c)).ToList() ?? [];
        return new ResultadoFerramentasExternas(capacidades);
    }

    [LoggerMessage(EventId = 1542, Level = LogLevel.Information, Message = "Ferramenta MCP {NomeFerramenta} ausente; etapa 6 concluída sem capacidades externas.")]
    private static partial void LogFerramentaAusente(ILogger logger, string nomeFerramenta);

    private sealed record RespostaJson(string? VersaoContrato, List<string>? Capacidades);
}
