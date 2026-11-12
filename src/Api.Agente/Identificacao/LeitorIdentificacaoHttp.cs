using System.Diagnostics.CodeAnalysis;
using Api.Agente.Erros;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Identificacao;
using Microsoft.Extensions.Primitives;

namespace Api.Agente.Identificacao;

/// <summary>
/// Lê <c>X-Usuario-Id</c>, <c>X-Usuario-Nome</c> e <c>X-Chamador-Id</c>, opcionais e não verificados na v1
/// (D-05; FR-052a, FR-052c). Com id ausente, <c>X-Usuario-Nome</c> usa a mesma regra do A2A. Cabeçalho
/// repetido ou fora do formato invalida a identificação.
/// </summary>
public static class LeitorIdentificacaoHttp
{
    public const string CabecalhoUsuario = "X-Usuario-Id";
    public const string CabecalhoUsuarioNome = "X-Usuario-Nome";
    public const string CabecalhoChamador = "X-Chamador-Id";

    private const string ChaveItem = "AuditoriaNFe.Identificacao";

    public static bool TentarLer(HttpRequest requisicao, [NotNullWhen(true)] out IdentificacaoSolicitante? identificacao)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        identificacao = null;
        if (!TentarValorUnico(requisicao.Headers[CabecalhoUsuario], out var usuario)
            || !TentarValorUnico(requisicao.Headers[CabecalhoUsuarioNome], out var usuarioNome)
            || !TentarValorUnico(requisicao.Headers[CabecalhoChamador], out var chamador))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(usuario))
        {
            return IdentificacaoSolicitante.TentarCriar(usuario, chamador, out identificacao)
                && identificacao is not null;
        }

        if (!string.IsNullOrEmpty(usuarioNome))
        {
            var chamadorEfetivo = string.IsNullOrEmpty(chamador)
                ? IdentificacaoSolicitante.ChamadorNaoInformado
                : chamador;
            return IdentificacaoSolicitante.TentarCriarPorNome(usuarioNome, chamadorEfetivo, out identificacao)
                && identificacao is not null;
        }

        return IdentificacaoSolicitante.TentarCriar(usuario, chamador, out identificacao)
            && identificacao is not null;
    }

    /// <summary>Identificação lida por <see cref="FiltroIdentificacao"/> para a requisição corrente.</summary>
    public static IdentificacaoSolicitante Obter(HttpContext contexto) =>
        contexto.Items[ChaveItem] as IdentificacaoSolicitante
            ?? throw new InvalidOperationException("A rota não usa o filtro de identificação.");

    internal static void Guardar(HttpContext contexto, IdentificacaoSolicitante identificacao) =>
        contexto.Items[ChaveItem] = identificacao;

    private static bool TentarValorUnico(StringValues valores, out string? valor)
    {
        valor = valores.Count == 1 ? valores[0] : null;
        return valores.Count <= 1;
    }
}

/// <summary>Rejeita com ERR-007 e HTTP 400, sem criar execução, a identificação inválida.</summary>
public sealed class FiltroIdentificacao : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!LeitorIdentificacaoHttp.TentarLer(context.HttpContext.Request, out var identificacao))
        {
            return MapeamentoErrosHttp.Erro(CodigoErro.MensagemForaDoContrato);
        }

        LeitorIdentificacaoHttp.Guardar(context.HttpContext, identificacao);
        return await next(context);
    }
}
