using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using Api.Agente.Identificacao;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Sessoes;

namespace Api.Agente.Erros;

/// <summary>
/// Status HTTP dos erros que rejeitam a requisição (plan.md, Canal direto). O corpo é o objeto
/// <c>erro</c> (<see cref="ErroRespostaV1"/>) ou o envelope da execução rejeitada (constitution 4.4).
/// Os demais códigos não são status HTTP: chegam dentro do envelope da execução.
/// </summary>
public static class MapeamentoErrosHttp
{
    private static readonly FrozenDictionary<string, int> Status = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [CodigoErro.SolicitacaoSemConteudo] = StatusCodes.Status400BadRequest,
        [CodigoErro.QuantidadeArquivosExcedida] = StatusCodes.Status400BadRequest,
        [CodigoErro.MensagemForaDoContrato] = StatusCodes.Status400BadRequest,
        [CodigoErro.JustificativaObrigatoria] = StatusCodes.Status400BadRequest,
        [CodigoErro.TamanhoExcedido] = StatusCodes.Status413PayloadTooLarge,
        [CodigoErro.SessaoNaoEncontrada] = StatusCodes.Status404NotFound,
        [CodigoErro.SessaoExpirada] = StatusCodes.Status410Gone,
        [CodigoErro.RecomendacaoJaDecidida] = StatusCodes.Status409Conflict,
        [CodigoErro.FalhaCheckpoint] = StatusCodes.Status503ServiceUnavailable,
        [CodigoErro.ArmazenamentoIndisponivel] = StatusCodes.Status503ServiceUnavailable,
        [CodigoErro.LimiteAtingido] = StatusCodes.Status429TooManyRequests,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlyCollection<string> CodigosMapeados => Status.Keys;

    public static int StatusPara(string codigo) =>
        Status.TryGetValue(codigo, out var status)
            ? status
            : throw new ArgumentOutOfRangeException(nameof(codigo), codigo, "Código sem status HTTP próprio; ele chega dentro do envelope.");

    /// <summary>Erro sem envelope; a mensagem padrão de <see cref="MensagensErro"/> é usada quando omitida.</summary>
    public static IResult Erro(
        string codigo,
        string? mensagem = null,
        Guid? execucaoId = null,
        string? arquivo = null,
        IReadOnlyDictionary<string, JsonElement>? detalhes = null)
    {
        var corpo = new ErroRespostaV1(execucaoId, new ErroV1(codigo, mensagem ?? MensagensErro.Para(codigo), arquivo, detalhes));
        return Results.Json(corpo, OpcoesJsonContrato.Padrao, statusCode: StatusPara(codigo));
    }

    /// <summary>Envelope de uma execução rejeitada, com o status do seu <c>erro</c>.</summary>
    public static IResult Envelope(EnvelopeV1 envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var codigo = envelope.Erro?.Codigo
            ?? throw new ArgumentException("O envelope não tem erro.", nameof(envelope));
        return Results.Json(envelope, OpcoesJsonContrato.Padrao, statusCode: StatusPara(codigo));
    }

    /// <summary>ERR-024 com <c>Retry-After</c> em segundos (mínimo 1).</summary>
    public static IResult LimiteAtingido(TimeSpan tempoParaNovaTentativa)
    {
        var segundos = (int)Math.Clamp(Math.Ceiling(tempoParaNovaTentativa.TotalSeconds), 1, int.MaxValue);
        return new ComRetryAfter(Erro(CodigoErro.LimiteAtingido, MensagensErro.LimiteAtingido(segundos)), segundos);
    }

    /// <summary>ERR-024 numa rota que inicia execução: envelope <c>Rejeitada</c> com <c>Retry-After</c>.</summary>
    public static IResult LimiteAtingidoExecucao(HttpContext contexto, TimeSpan tempoParaNovaTentativa)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        var segundos = (int)Math.Clamp(Math.Ceiling(tempoParaNovaTentativa.TotalSeconds), 1, int.MaxValue);
        var servicos = contexto.RequestServices;
        LeitorIdentificacaoHttp.TentarLer(contexto.Request, out var identificacao);
        var envelope = MontadorEnvelope.Rejeitada(
            servicos.GetRequiredService<IGeradorIds>().Novo(),
            identificacao,
            CanalExecucao.Direto,
            servicos.GetRequiredService<IRelogio>().Agora,
            CodigoErro.LimiteAtingido,
            MensagensErro.LimiteAtingido(segundos));
        return new ComRetryAfter(Envelope(envelope), segundos);
    }

    private sealed class ComRetryAfter(IResult interno, int segundos) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
            return interno.ExecuteAsync(httpContext);
        }
    }
}
