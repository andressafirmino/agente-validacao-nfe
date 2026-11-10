using System.Text.Json;
using Api.Agente.Erros;
using Api.Agente.Identificacao;
using AuditoriaNFe.Application.CasosDeUso;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.ContextoUsuario;

namespace Api.Agente.Endpoints;

/// <summary>
/// Consulta, correção e exclusão do contexto do usuário pelo canal direto (FR-059), isolado por
/// usuário e chamador (FR-056). Remoções de item ou contexto inexistente respondem 204.
/// </summary>
public static class ContextoUsuarioEndpoints
{
    public const string Rota = "/contexto-usuario";

    private static readonly string[] CamposPreferencias = ["nivelDetalhe", "tributosInteresse"];

    public static RouteGroupBuilder MapearContextoUsuario(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        api.MapGet(Rota, ObterAsync)
            .WithName("obterContextoUsuario")
            .AddEndpointFilter<FiltroIdentificacao>()
            .Produces<ContextoUsuarioV1>(StatusCodes.Status200OK)
            .Produces<ErroRespostaV1>(StatusCodes.Status404NotFound);

        api.MapDelete(Rota, ExcluirAsync)
            .WithName("excluirContextoUsuario")
            .AddEndpointFilter<FiltroIdentificacao>()
            .Produces(StatusCodes.Status204NoContent);

        api.MapPut($"{Rota}/preferencias", DeclararPreferenciasAsync)
            .WithName("declararPreferencias")
            .AddEndpointFilter<FiltroIdentificacao>()
            .Accepts<PreferenciasEntradaV1>("application/json")
            .Produces<ContextoUsuarioV1>(StatusCodes.Status200OK)
            .Produces<ErroRespostaV1>(StatusCodes.Status400BadRequest);

        api.MapDelete($"{Rota}/cnpjs/{{cnpj}}", RemoverCnpjAsync)
            .WithName("removerCnpjDoContexto")
            .AddEndpointFilter<FiltroIdentificacao>()
            .Produces(StatusCodes.Status204NoContent);

        api.MapDelete($"{Rota}/achados-recorrentes/{{chave}}", RemoverAchadoRecorrenteAsync)
            .WithName("removerAchadoRecorrente")
            .AddEndpointFilter<FiltroIdentificacao>()
            .Produces(StatusCodes.Status204NoContent);

        return api;
    }

    private static async Task<IResult> ObterAsync(HttpContext contexto, GerirContextoUsuario casoDeUso, CancellationToken cancellationToken)
    {
        var contextoUsuario = await casoDeUso.ObterAsync(LeitorIdentificacaoHttp.Obter(contexto), cancellationToken);
        return contextoUsuario is null
            ? MapeamentoErrosHttp.Erro(CodigoErro.SessaoNaoEncontrada, MensagensErro.ContextoUsuarioNaoEncontrado)
            : Results.Json(ContextoUsuarioV1.De(contextoUsuario), OpcoesJsonContrato.Padrao);
    }

    private static async Task<IResult> ExcluirAsync(HttpContext contexto, GerirContextoUsuario casoDeUso, CancellationToken cancellationToken)
    {
        await casoDeUso.ExcluirAsync(LeitorIdentificacaoHttp.Obter(contexto), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DeclararPreferenciasAsync(HttpContext contexto, GerirContextoUsuario casoDeUso, CancellationToken cancellationToken)
    {
        var identificacao = LeitorIdentificacaoHttp.Obter(contexto);
        if (!identificacao.UsuarioIdentificado)
        {
            return MapeamentoErrosHttp.Erro(CodigoErro.MensagemForaDoContrato, MensagensErro.UsuarioSemContexto);
        }

        if (await LerPreferenciasAsync(contexto.Request, cancellationToken) is not { } entrada)
        {
            return MapeamentoErrosHttp.Erro(CodigoErro.MensagemForaDoContrato, MensagensErro.PreferenciasForaDoContrato);
        }

        var contextoUsuario = await casoDeUso.DeclararPreferenciasAsync(identificacao, entrada.Nivel, entrada.Tributos, cancellationToken);
        return Results.Json(ContextoUsuarioV1.De(contextoUsuario), OpcoesJsonContrato.Padrao);
    }

    /// <summary>CNPJ fora do formato ou papel fora da lista não correspondem a item nenhum: nada é removido.</summary>
    private static async Task<IResult> RemoverCnpjAsync(
        HttpContext contexto,
        string cnpj,
        string? papel,
        GerirContextoUsuario casoDeUso,
        CancellationToken cancellationToken)
    {
        PapelCnpj? papelCnpj = null;
        if (papel is not null)
        {
            if (!Enum.GetNames<PapelCnpj>().Contains(papel, StringComparer.Ordinal))
            {
                return Results.NoContent();
            }

            papelCnpj = Enum.Parse<PapelCnpj>(papel);
        }

        if (ContextoUsuario.CnpjValido(cnpj))
        {
            await casoDeUso.RemoverCnpjAsync(LeitorIdentificacaoHttp.Obter(contexto), cnpj, papelCnpj, cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> RemoverAchadoRecorrenteAsync(
        HttpContext contexto,
        string chave,
        GerirContextoUsuario casoDeUso,
        CancellationToken cancellationToken)
    {
        if (ContextoUsuario.ChaveAchadoValida(chave))
        {
            await casoDeUso.RemoverAchadoRecorrenteAsync(LeitorIdentificacaoHttp.Obter(contexto), chave, cancellationToken);
        }

        return Results.NoContent();
    }

    /// <summary>
    /// <c>PreferenciasEntrada</c> sem campos extras, com <c>nivelDetalhe</c> e <c>tributosInteresse</c>
    /// da lista fechada e sem repetição; <see langword="null"/> fora do contrato.
    /// </summary>
    private static async Task<(NivelDetalhe Nivel, string[] Tributos)?> LerPreferenciasAsync(HttpRequest requisicao, CancellationToken cancellationToken)
    {
        if (!requisicao.HasJsonContentType())
        {
            return null;
        }

        JsonDocument documento;
        try
        {
            documento = await JsonDocument.ParseAsync(requisicao.Body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }

        using (documento)
        {
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object
                || !raiz.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(CamposPreferencias, StringComparer.Ordinal)
                || raiz.GetProperty("nivelDetalhe") is not { ValueKind: JsonValueKind.String } nivel
                || !Enum.GetNames<NivelDetalhe>().Contains(nivel.GetString(), StringComparer.Ordinal)
                || raiz.GetProperty("tributosInteresse") is not { ValueKind: JsonValueKind.Array } lista
                || lista.EnumerateArray().Any(t => t.ValueKind != JsonValueKind.String || !TributosInteresse.Permitido(t.GetString())))
            {
                return null;
            }

            var tributos = lista.EnumerateArray().Select(t => t.GetString()!).ToArray();
            return tributos.Distinct(StringComparer.Ordinal).Count() == tributos.Length
                ? (Enum.Parse<NivelDetalhe>(nivel.GetString()!), tributos)
                : null;
        }
    }
}
