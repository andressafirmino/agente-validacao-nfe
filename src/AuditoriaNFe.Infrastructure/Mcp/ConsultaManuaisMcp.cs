using System.Globalization;
using System.Text.Json;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AuditoriaNFe.Infrastructure.Mcp;

/// <summary>
/// Consulta a manuais oficiais pela ferramenta MCP <c>buscar_trechos_manual</c> da Api.RAG
/// (contracts/consulta-manuais-mcp.md; FR-047a a FR-047d, R-13): Streamable HTTP, limite de
/// <c>Mcp:Timeout</c> por consulta, uma nova tentativa só em falha de transporte e qualquer falha
/// como <see cref="ConsultaManuaisIndisponivelException"/> (ERR-013).
/// </summary>
public sealed partial class ConsultaManuaisMcp(IOptions<McpOpcoes> opcoes, ILoggerFactory logs) : IConsultaManuais
{
    public const string NomeFerramenta = "buscar_trechos_manual";
    public static readonly IReadOnlySet<string> VersoesSuportadas = new HashSet<string>(["0.1"], StringComparer.Ordinal);

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);
    private readonly ILogger _logger = logs.CreateLogger<ConsultaManuaisMcp>();

    public bool Habilitada => true;

    public async Task<ResultadoConsultaManuais> ConsultarAsync(ConsultaManuais consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var configuracao = opcoes.Value;
        if (!Uri.TryCreate(configuracao.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new ConsultaManuaisIndisponivelException("Mcp:Endpoint ausente ou inválido.");
        }

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(configuracao.Timeout);
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                return await ConsultarUmaVezAsync(endpoint, configuracao.Timeout, consulta, limite.Token).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (tentativa == 1 && !limite.IsCancellationRequested)
            {
                LogNovaTentativa(_logger);
            }
            catch (OperationCanceledException erro) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ConsultaManuaisIndisponivelException("A consulta a manuais excedeu Mcp:Timeout.", erro);
            }
            catch (Exception erro) when (erro is not ConsultaManuaisIndisponivelException and not OperationCanceledException and not OutOfMemoryException)
            {
                throw new ConsultaManuaisIndisponivelException("A consulta a manuais falhou.", erro);
            }
        }
    }

    private async Task<ResultadoConsultaManuais> ConsultarUmaVezAsync(Uri endpoint, TimeSpan timeout, ConsultaManuais consulta, CancellationToken cancellationToken)
    {
        var transporte = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = endpoint,
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = timeout,
            },
            logs);
        await using var cliente = await McpClient.CreateAsync(transporte, loggerFactory: logs, cancellationToken: cancellationToken).ConfigureAwait(false);

        var ferramentas = await cliente.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!ferramentas.Any(f => f.Name == NomeFerramenta))
        {
            throw new ConsultaManuaisIndisponivelException($"Ferramenta {NomeFerramenta} ausente no servidor MCP.");
        }

        var argumentos = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tipoDocumento"] = consulta.TipoDocumento,
            ["modelo"] = consulta.Modelo,
            ["versaoLayout"] = consulta.VersaoLayout,
            ["dataReferencia"] = consulta.DataReferencia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["consulta"] = consulta.Consulta,
            ["regrasRelacionadas"] = consulta.RegrasRelacionadas.ToArray(),
            ["quantidadeMaxima"] = consulta.QuantidadeMaxima,
        };
        var resultado = await cliente.CallToolAsync(NomeFerramenta, argumentos, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (resultado.IsError == true)
        {
            throw new ConsultaManuaisIndisponivelException("O servidor MCP devolveu erro na ferramenta.");
        }

        return Interpretar(resultado);
    }

    private static ResultadoConsultaManuais Interpretar(CallToolResult resultado)
    {
        var json = resultado.StructuredContent is { } estruturado
            ? JsonSerializer.Serialize(estruturado, OpcoesJson)
            : resultado.Content.OfType<TextContentBlock>().Select(t => t.Text).FirstOrDefault()
                ?? throw new ConsultaManuaisIndisponivelException("Resposta MCP sem conteúdo.");

        RespostaJson resposta;
        try
        {
            resposta = JsonSerializer.Deserialize<RespostaJson>(json, OpcoesJson)
                ?? throw new ConsultaManuaisIndisponivelException("Resposta MCP vazia.");
        }
        catch (JsonException erro)
        {
            throw new ConsultaManuaisIndisponivelException("Resposta MCP fora do contrato.", erro);
        }

        if (resposta.VersaoContrato is null || !VersoesSuportadas.Contains(resposta.VersaoContrato))
        {
            throw new ConsultaManuaisIndisponivelException("Versão do contrato MCP não suportada.");
        }

        var trechos = (resposta.Trechos ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.ChunkId) && !string.IsNullOrWhiteSpace(t.Documento)
                && !string.IsNullOrWhiteSpace(t.Versao) && !string.IsNullOrWhiteSpace(t.Trecho)
                && Data(t.Vigencia?.Inicio) is not null)
            .Select(t => new TrechoManual(
                t.ChunkId!,
                t.Documento!,
                t.Versao!,
                Data(t.Vigencia!.Inicio)!.Value,
                Data(t.Vigencia.Fim),
                t.Trecho!,
                t.Localizacao?.Secao,
                t.Localizacao?.Pagina,
                t.Pontuacao))
            .ToList();
        return new ResultadoConsultaManuais(trechos);
    }

    private static DateOnly? Data(string? texto) =>
        DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data : null;

    [LoggerMessage(EventId = 1541, Level = LogLevel.Warning, Message = "Falha de transporte na consulta a manuais; nova tentativa.")]
    private static partial void LogNovaTentativa(ILogger logger);

    private sealed record RespostaJson(string? VersaoContrato, List<TrechoJson>? Trechos);

    private sealed record TrechoJson(
        string? ChunkId,
        string? Documento,
        string? Versao,
        VigenciaJson? Vigencia,
        LocalizacaoJson? Localizacao,
        string? Trecho,
        double? Pontuacao);

    private sealed record VigenciaJson(string? Inicio, string? Fim);

    private sealed record LocalizacaoJson(string? Secao, int? Pagina);
}
