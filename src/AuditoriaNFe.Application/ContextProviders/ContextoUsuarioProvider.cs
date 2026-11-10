using AuditoriaNFe.Domain.ContextoUsuario;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AuditoriaNFe.Application.ContextProviders;

/// <summary>
/// Contexto do usuário entregue ao agente (FR-058): só CNPJs com contagem, chaves de achados
/// recorrentes e preferências, limitados por <c>ContextoUsuario:MaxItensNoPrompt</c>. Não é evidência
/// nem participa do resultado geral.
/// </summary>
public sealed record ContextoUsuarioIa(
    IReadOnlyList<CnpjContextoIa> CnpjsAuditados,
    IReadOnlyList<AchadoRecorrenteContextoIa> AchadosRecorrentes,
    PreferenciasContextoIa? Preferencias)
{
    /// <summary>CNPJs mais recentes e achados mais frequentes primeiro.</summary>
    public static ContextoUsuarioIa De(ContextoUsuario contexto, int maxItens)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentOutOfRangeException.ThrowIfNegative(maxItens);
        return new ContextoUsuarioIa(
            [.. contexto.CnpjsAuditados
                .OrderByDescending(c => c.UltimaAuditoriaEm)
                .Take(maxItens)
                .Select(c => new CnpjContextoIa(c.Cnpj, c.Papel.ToString(), c.QuantidadeDocumentos))],
            [.. contexto.AchadosRecorrentes
                .OrderByDescending(a => a.Frequencia)
                .ThenByDescending(a => a.UltimaOcorrenciaEm)
                .Take(maxItens)
                .Select(a => new AchadoRecorrenteContextoIa(a.Chave, a.Categoria.ToString(), a.Frequencia))],
            contexto.Preferencias is { } preferencias
                ? new PreferenciasContextoIa(preferencias.NivelDetalhe.ToString(), [.. preferencias.TributosInteresse])
                : null);
    }
}

public sealed record CnpjContextoIa(string Cnpj, string Papel, int QuantidadeDocumentos);

public sealed record AchadoRecorrenteContextoIa(string Chave, string Categoria, int Frequencia);

public sealed record PreferenciasContextoIa(string NivelDetalhe, IReadOnlyList<string> TributosInteresse);

/// <summary>
/// Entrega o <see cref="ContextoUsuarioIa"/> como dado delimitado e não confiável (research R-07).
/// Só leitura: não sobrescreve <c>StoreAIContextAsync</c>, e nada da resposta da IA chega ao contexto
/// do usuário (FR-057). Não é usado para "anonimo".
/// </summary>
public sealed class ContextoUsuarioProvider : AIContextProvider
{
    public const string InicioContexto = "<<<CONTEXTO_USUARIO>>>";
    public const string FimContexto = "<<<FIM_CONTEXTO_USUARIO>>>";

    private readonly ContextoUsuarioIa _contexto;

    public ContextoUsuarioProvider(ContextoUsuarioIa contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _contexto = contexto;
    }

    public static string Montar(ContextoUsuarioIa contexto) => TextoDelimitado.Montar(InicioContexto, FimContexto, contexto);

    protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new AIContext { Messages = [new ChatMessage(ChatRole.User, Montar(_contexto))] });
}
