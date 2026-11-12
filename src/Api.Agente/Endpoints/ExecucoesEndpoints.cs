using Api.Agente.Documentacao;
using Api.Agente.Erros;
using Api.Agente.Identificacao;
using Api.Agente.Limites;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.CasosDeUso;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Application.Fluxo;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Sessoes;
namespace Api.Agente.Endpoints;

/// <summary>
/// Canal direto (contracts/api-agente.openapi.yaml): <c>POST /api/v1/execucoes</c> em multipart e
/// <c>GET /api/v1/execucoes/{execucaoId}</c>, que devolve o envelope gravado ao mesmo solicitante.
/// </summary>
public static class ExecucoesEndpoints
{
    public const string Rota = "/execucoes";

    public static RouteGroupBuilder MapearExecucoes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapPost(Rota, CriarAsync)
            .WithName("criarExecucao")
            .DisableAntiforgery()
            .AddEndpointFilter<FiltroIdentificacao>()
            .ComLimitesDeExecucao()
            .AplicarCorpoMultipartExecucaoOpenApi()
            .AplicarIdentificacaoOpenApi()
            .Produces<EnvelopeV1>(StatusCodes.Status200OK)
            .Produces<EnvelopeV1>(StatusCodes.Status400BadRequest)
            .Produces<EnvelopeV1>(StatusCodes.Status404NotFound)
            .Produces<EnvelopeV1>(StatusCodes.Status410Gone)
            .Produces<EnvelopeV1>(StatusCodes.Status413PayloadTooLarge)
            .Produces<EnvelopeV1>(StatusCodes.Status429TooManyRequests)
            .Produces<EnvelopeV1>(StatusCodes.Status503ServiceUnavailable);

        api.MapGet($"{Rota}/{{execucaoId:guid}}", ObterAsync)
            .WithName("obterExecucao")
            .AddEndpointFilter<FiltroIdentificacao>()
            .AplicarIdentificacaoOpenApi()
            .Produces<EnvelopeV1>(StatusCodes.Status200OK)
            .Produces<ErroRespostaV1>(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> CriarAsync(
        HttpContext contexto,
        ExecutarSolicitacaoAuditoria casoDeUso,
        CancellationToken cancellationToken)
    {
        var identificacao = LeitorIdentificacaoHttp.Obter(contexto);
        var solicitacao = await LerAsync(contexto.Request, identificacao, cancellationToken);
        var resultado = solicitacao is null
            ? casoDeUso.RejeitarForaDoContrato(identificacao, CanalExecucao.Direto)
            : await casoDeUso.ExecutarAsync(solicitacao, cancellationToken);

        return resultado.Rejeitada
            ? MapeamentoErrosHttp.Envelope(resultado.Envelope)
            : Results.Json(resultado.Envelope, OpcoesJsonContrato.Padrao);
    }

    /// <summary>
    /// Campos do multipart; <see langword="null"/> quando a mensagem não segue o contrato. Formulário
    /// sem nenhuma parte legível equivale a solicitação vazia (ERR-001).
    /// </summary>
    private static async Task<SolicitacaoAuditoria?> LerAsync(
        HttpRequest requisicao,
        IdentificacaoSolicitante identificacao,
        CancellationToken cancellationToken)
    {
        IFormCollection formulario;
        if (!requisicao.HasFormContentType)
        {
            formulario = FormCollection.Empty;
        }
        else
        {
            try
            {
                formulario = await requisicao.ReadFormAsync(cancellationToken);
            }
            catch (InvalidDataException)
            {
                formulario = FormCollection.Empty;
            }
        }

        var pergunta = formulario["pergunta"];
        var sessao = formulario["sessaoId"];
        var correlacao = formulario["correlacaoId"];
        if (pergunta.Count > 1 || sessao.Count > 1 || correlacao.Count > 1)
        {
            return null;
        }

        Guid? sessaoId = null;
        if (!string.IsNullOrEmpty(sessao.ToString()))
        {
            if (!Guid.TryParse(sessao.ToString(), out var id))
            {
                return null;
            }

            sessaoId = id;
        }

        var arquivos = new List<ArquivoSolicitacao>();
        foreach (var arquivo in formulario.Files.GetFiles("arquivos"))
        {
            using var memoria = new MemoryStream((int)Math.Min(arquivo.Length, int.MaxValue));
            await arquivo.CopyToAsync(memoria, cancellationToken);
            arquivos.Add(new ArquivoSolicitacao(Path.GetFileName(arquivo.FileName), memoria.ToArray()));
        }

        var textoPergunta = pergunta.ToString();
        var textoCorrelacao = correlacao.ToString();
        return new SolicitacaoAuditoria(
            identificacao,
            CanalExecucao.Direto,
            string.IsNullOrWhiteSpace(textoPergunta) ? null : textoPergunta,
            arquivos,
            sessaoId,
            string.IsNullOrEmpty(textoCorrelacao) ? null : textoCorrelacao);
    }

    private static async Task<IResult> ObterAsync(
        HttpContext contexto,
        Guid execucaoId,
        IExecucaoRepositorio execucoes,
        CancellationToken cancellationToken)
    {
        var identificacao = LeitorIdentificacaoHttp.Obter(contexto);
        var execucao = await execucoes.ObterAsync(execucaoId, identificacao.ChaveIsolamento, cancellationToken);
        return execucao is null
            ? MapeamentoErrosHttp.Erro(CodigoErro.SessaoNaoEncontrada, "Execução não encontrada.")
            : Results.Json(MontadorEnvelope.De(execucao), OpcoesJsonContrato.Padrao);
    }
}
