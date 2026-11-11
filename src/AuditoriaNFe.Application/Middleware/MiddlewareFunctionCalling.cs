using System.Diagnostics;
using System.Text.Json;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Ferramentas;
using AuditoriaNFe.Domain.Sessoes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Application.Middleware;

/// <summary>
/// Middleware de Function Calling (research R-09): lista permitida, timeout, registro em
/// <see cref="Execucao.ChamadasFerramentas"/> e <c>context.Terminate</c> em violação (P10).
/// </summary>
public sealed partial class MiddlewareFunctionCalling(
    IOptions<FerramentasOpcoes> ferramentas,
    IRelogio relogio,
    ILogger<MiddlewareFunctionCalling> logger)
{
    public AIAgent Aplicar(AIAgent agente)
    {
        ArgumentNullException.ThrowIfNull(agente);
        return agente.AsBuilder().Use(InvocarAsync).Build();
    }

    private async ValueTask<object?> InvocarAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var escopo = EscopoFerramentasExecucao.Atual;
        var nome = context.Function.Name;
        var inicio = relogio.Agora;
        var relogioMs = Stopwatch.GetTimestamp();

        if (escopo is null)
        {
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.FalhaInterna, "Escopo de ferramentas ausente.");
        }

        if (!PoliticaFerramentas.Autorizada(escopo.Execucao.Tipo, nome))
        {
            Registrar(escopo.Execucao, nome, inicio, relogioMs, StatusChamadaFerramenta.NaoAutorizada, CodigoErroFerramenta.NaoAutorizada);
            context.Terminate = true;
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.NaoAutorizada, "Ferramenta não autorizada para este tipo de execução.");
        }

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(ferramentas.Value.Timeout);
        try
        {
            var resultado = await next(context, limite.Token).ConfigureAwait(false);
            Guid? calculoId = null;
            if (nome == PoliticaFerramentas.CalcularTotal)
            {
                calculoId = ExtrairCalculoId(resultado) ?? escopo.Execucao.Calculos.LastOrDefault()?.Id;
            }

            Registrar(escopo.Execucao, nome, inicio, relogioMs, StatusChamadaFerramenta.Sucesso, null, calculoId);
            return resultado;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Registrar(escopo.Execucao, nome, inicio, relogioMs, StatusChamadaFerramenta.Timeout, CodigoErroFerramenta.Timeout);
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.Timeout, "Tempo esgotado na ferramenta.");
        }
        catch (Exception erro) when (erro is not OperationCanceledException and not OutOfMemoryException)
        {
            LogFalha(logger, escopo.Execucao.Id, nome, erro.GetType().Name);
            Registrar(escopo.Execucao, nome, inicio, relogioMs, StatusChamadaFerramenta.Falha, CodigoErroFerramenta.FalhaInterna);
            return ResultadoFerramenta.Erro(CodigoErroFerramenta.FalhaInterna, "Falha interna na ferramenta.");
        }
    }

    private static void Registrar(
        Execucao execucao,
        string nome,
        DateTimeOffset inicio,
        long inicioRelogio,
        StatusChamadaFerramenta status,
        string? erro,
        Guid? calculoId = null)
    {
        var duracao = (long)Stopwatch.GetElapsedTime(inicioRelogio).TotalMilliseconds;
        execucao.RegistrarChamadaFerramenta(new ChamadaFerramenta(nome, inicio, duracao, status, erro, calculoId));
    }

    private static Guid? ExtrairCalculoId(object? resultado)
    {
        if (resultado is null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(resultado);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("calculoId", out var id) && Guid.TryParse(id.GetString(), out var guid))
            {
                return guid;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    [LoggerMessage(EventId = 1504, Level = LogLevel.Warning,
        Message = "Falha na ferramenta {Nome} na execução {ExecucaoId} ({TipoErro}).")]
    private static partial void LogFalha(ILogger logger, Guid execucaoId, string nome, string tipoErro);
}
