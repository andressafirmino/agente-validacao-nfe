using System.Threading.RateLimiting;
using Api.Agente.Erros;
using Api.Agente.Identificacao;
using AuditoriaNFe.Application.Configuracao;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace Api.Agente.Limites;

/// <summary>Marca as rotas que iniciam execução, sujeitas a <c>Concorrencia:MaxExecucoes</c>.</summary>
public sealed class LimiteConcorrenciaExecucao
{
    public static readonly LimiteConcorrenciaExecucao Instancia = new();

    private LimiteConcorrenciaExecucao()
    {
    }
}

/// <summary>
/// Limite de corpo (<c>Limites:CorpoHttpBytes</c>), taxa em janela fixa por <c>chaveIsolamento</c>
/// (<c>LimiteTaxa</c>) nas rotas de análise e de decisão e concorrência das execuções
/// (<c>Concorrencia:MaxExecucoes</c>). A rejeição pelos dois limites é ERR-024 com <c>Retry-After</c>.
/// </summary>
public static class LimitesHttpExtensions
{
    public const string PoliticaTaxa = "taxa-por-solicitante";

    private const string ParticaoIdentificacaoInvalida = "identificacao-invalida";
    private static readonly TimeSpan EsperaConcorrencia = TimeSpan.FromSeconds(1);

    public static IServiceCollection AdicionarLimitesHttp(this IServiceCollection services)
    {
        services.AddOptions<KestrelServerOptions>()
            .Configure<IOptions<LimitesOpcoes>>((kestrel, limites) => kestrel.Limits.MaxRequestBodySize = limites.Value.CorpoHttpBytes);
        services.AddOptions<FormOptions>()
            .Configure<IOptions<LimitesOpcoes>>((formulario, limites) => formulario.MultipartBodyLengthLimit = limites.Value.CorpoHttpBytes);

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<LimiteTaxaOpcoes>, IOptions<ConcorrenciaOpcoes>>((opcoes, taxa, concorrencia) =>
            {
                opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                opcoes.AddPolicy(PoliticaTaxa, contexto => RateLimitPartition.GetFixedWindowLimiter(
                    Particao(contexto),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = taxa.Value.PermissoesPorJanela,
                        Window = taxa.Value.Janela,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
                opcoes.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
                    contexto.GetEndpoint()?.Metadata.GetMetadata<LimiteConcorrenciaExecucao>() is null
                        ? RateLimitPartition.GetNoLimiter(string.Empty)
                        : RateLimitPartition.GetConcurrencyLimiter(
                            "execucoes",
                            _ => new ConcurrencyLimiterOptions { PermitLimit = concorrencia.Value.MaxExecucoes, QueueLimit = 0 }));
                opcoes.OnRejected = (rejeicao, _) =>
                {
                    var espera = rejeicao.Lease.TryGetMetadata(MetadataName.RetryAfter, out var tempo) ? tempo : EsperaConcorrencia;
                    var contexto = rejeicao.HttpContext;
                    var resultado = contexto.GetEndpoint()?.Metadata.GetMetadata<LimiteConcorrenciaExecucao>() is null
                        ? MapeamentoErrosHttp.LimiteAtingido(espera)
                        : MapeamentoErrosHttp.LimiteAtingidoExecucao(contexto, espera);
                    return new ValueTask(resultado.ExecuteAsync(contexto));
                };
            });

        return services;
    }

    /// <summary>Rotas de decisão: limite de taxa por solicitante.</summary>
    public static TBuilder ComLimiteDeTaxa<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireRateLimiting(PoliticaTaxa);

    /// <summary>Rotas que iniciam execução: limite de taxa por solicitante e de concorrência.</summary>
    public static TBuilder ComLimitesDeExecucao<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireRateLimiting(PoliticaTaxa).WithMetadata(LimiteConcorrenciaExecucao.Instancia);

    private static string Particao(HttpContext contexto) =>
        LeitorIdentificacaoHttp.TentarLer(contexto.Request, out var identificacao)
            ? identificacao.ChaveIsolamento
            : ParticaoIdentificacaoInvalida;
}
