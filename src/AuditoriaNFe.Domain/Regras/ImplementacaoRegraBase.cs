using System.Globalization;

namespace AuditoriaNFe.Domain.Regras;

/// <summary>Base das implementações: id <c>{regra}.v{versao}</c>, campos lidos e comparação com tolerância.</summary>
internal abstract class ImplementacaoRegraBase(string id, IReadOnlyList<string> campos) : IImplementacaoRegra
{
    public string Id { get; } = id;

    public IReadOnlyList<string> Campos { get; } = campos;

    public AvaliacaoRegra Avaliar(DadosDocumento documento, RegraConteudo regra)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(regra);
        return Avaliar(LeituraDocumento.De(documento), regra);
    }

    protected abstract AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra);

    /// <summary>Conforme quando a diferença não passa da tolerância da regra; sem tolerância, igualdade exata.</summary>
    protected static AvaliacaoRegra Comparar(string campo, decimal informado, decimal calculado, RegraConteudo regra) =>
        Math.Abs(informado - calculado) <= (regra.Tolerancia ?? 0m)
            ? AvaliacaoRegra.Conforme()
            : AvaliacaoRegra.Divergente(new DivergenciaRegra(campo, Texto(informado), Texto(calculado)));

    protected static string Texto(decimal valor) => valor.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Total igual à soma de um campo dos itens, com filtro de itens e grupo de total exigido opcionais.</summary>
internal sealed class RegraSomaItens(
    string id,
    string total,
    string campoItem,
    IReadOnlyList<string>? camposFiltro = null,
    Func<ItemDocumento, bool>? filtro = null,
    string? grupoExigido = null)
    : ImplementacaoRegraBase(id, [total, "det[*]/" + campoItem, .. camposFiltro ?? []])
{
    protected override AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra)
    {
        if (grupoExigido is not null && !documento.TemGrupo(grupoExigido))
        {
            return AvaliacaoRegra.NaoAplicavel($"Grupo {grupoExigido.TrimEnd('/')} ausente no documento.");
        }

        return Comparar(total, documento.ValorOuZero(total), documento.SomaItens(campoItem, filtro), regra);
    }
}

/// <summary>Filtros de itens e campos que identificam os grupos de tributo do item.</summary>
internal static class GruposItem
{
    public const string CstIcms = "det[*]/imposto/ICMS/*/CST";
    public const string CsosnIcms = "det[*]/imposto/ICMS/*/CSOSN";
    public const string ServicoIssqn = "det[*]/imposto/ISSQN/cListServ";
    public const string CstIbsCbs = "det[*]/imposto/IBSCBS/CST";
    public const string CstIs = "det[*]/imposto/IS/CSTIS";
    public const string IndTot = "det[*]/prod/indTot";

    public static readonly IReadOnlyList<string> CamposIcms = [CstIcms, CsosnIcms];

    public static bool ComIcms(ItemDocumento item) => item.TemGrupo("imposto/ICMS/");

    public static bool ComIssqn(ItemDocumento item) => item.TemGrupo("imposto/ISSQN/");

    public static bool ComIbsCbs(ItemDocumento item) => item.TemGrupo("imposto/IBSCBS/");

    public static bool ComIs(ItemDocumento item) => item.TemGrupo("imposto/IS/");

    public static bool CompoeTotal(ItemDocumento item) => item.Texto("prod/indTot") == "1";
}
