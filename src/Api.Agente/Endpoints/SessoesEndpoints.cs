using Api.Agente.Documentacao;
using Api.Agente.Erros;
using Api.Agente.Identificacao;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Application.Sessoes;

namespace Api.Agente.Endpoints;

/// <summary>
/// <c>DELETE /api/v1/sessoes/{sessaoId}</c> (FR-041): encerra a sessão do mesmo solicitante. Sessão de
/// outro usuário ou chamador responde ERR-005 como se não existisse (FR-039); encerrar de novo uma
/// sessão encerrada ou expirada responde 204.
/// </summary>
public static class SessoesEndpoints
{
    public const string Rota = "/sessoes";

    public static RouteGroupBuilder MapearSessoes(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapDelete($"{Rota}/{{sessaoId:guid}}", EncerrarAsync)
            .WithName("encerrarSessao")
            .AddEndpointFilter<FiltroIdentificacao>()
            .AplicarIdentificacaoOpenApi()
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErroRespostaV1>(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> EncerrarAsync(
        HttpContext contexto,
        Guid sessaoId,
        ServicoSessoes sessoes,
        IRelogio relogio,
        CancellationToken cancellationToken)
    {
        var identificacao = LeitorIdentificacaoHttp.Obter(contexto);
        return await sessoes.EncerrarAsync(sessaoId, identificacao, relogio.Agora, cancellationToken)
            ? Results.NoContent()
            : MapeamentoErrosHttp.Erro(CodigoErro.SessaoNaoEncontrada);
    }
}
