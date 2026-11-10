namespace AuditoriaNFe.Domain.Regras.IbsCbsIs;

/// <summary>
/// Regras de IS, IBS e CBS da NT 2025.002 v1.53 (pp. 70–71), aplicadas só quando os grupos desses
/// tributos estiverem preenchidos no documento.
/// </summary>
internal static class RegrasIbsCbsIs
{
    private const string Total = "total/IBSCBSTot/";
    private const string Item = "imposto/IBSCBS/";

    public static IReadOnlyList<IImplementacaoRegra> Todas { get; } =
    [
        new RegraPresencaTotal("W31-10.v1", "total/ISTot", "total/ISTot/vIS", GruposItem.CstIs, GruposItem.ComIs, informadoSemItem: true),
        new RegraPresencaTotal("W31-20.v1", "total/ISTot", "total/ISTot/vIS", GruposItem.CstIs, GruposItem.ComIs, informadoSemItem: false),
        new RegraPresencaTotal("W34-10.v1", "total/IBSCBSTot", Total + "vBCIBSCBS", GruposItem.CstIbsCbs, GruposItem.ComIbsCbs, informadoSemItem: true),
        new RegraPresencaTotal("W34-20.v1", "total/IBSCBSTot", Total + "vBCIBSCBS", GruposItem.CstIbsCbs, GruposItem.ComIbsCbs, informadoSemItem: false),
        Soma("W35-10", "vBCIBSCBS", "gIBSCBS/vBC"),
        Soma("W38-10", "gIBS/gIBSUF/vDif", "gIBSCBS/gIBSUF/gDif/vDif"),
        Soma("W39-10", "gIBS/gIBSUF/vDevTrib", "gIBSCBS/gIBSUF/gDevTrib/vDevTrib"),
        Soma("W41-10", "gIBS/gIBSUF/vIBSUF", "gIBSCBS/gIBSUF/vIBSUF"),
        Soma("W43-10", "gIBS/gIBSMun/vDif", "gIBSCBS/gIBSMun/gDif/vDif"),
        Soma("W44-10", "gIBS/gIBSMun/vDevTrib", "gIBSCBS/gIBSMun/gDevTrib/vDevTrib"),
        Soma("W46-10", "gIBS/gIBSMun/vIBSMun", "gIBSCBS/gIBSMun/vIBSMun"),
        Soma("W47-10", "gIBS/vIBS", "gIBSCBS/vIBS"),
        Soma("W48-10", "gIBS/vCredPres", "gCredPresOper/gIBSCredPres/vCredPres"),
        Soma("W53-10", "gCBS/vDif", "gIBSCBS/gCBS/gDif/vDif"),
        Soma("W54-10", "gCBS/vDevTrib", "gIBSCBS/gCBS/gDevTrib/vDevTrib"),
        Soma("W56-10", "gCBS/vCBS", "gIBSCBS/gCBS/vCBS"),
        Soma("W56a-10", "gCBS/vCredPres", "gCredPresOper/gCBSCredPres/vCredPres"),
        Soma("W58-10", "gMono/vIBSMono", "gIBSCBSMono/gMonoPadrao/vIBSMono"),
        Soma("W59-10", "gMono/vCBSMono", "gIBSCBSMono/gMonoPadrao/vCBSMono"),
        Soma("W59a-10", "gMono/vIBSMonoReten", "gIBSCBSMono/gMonoReten/vIBSMonoReten"),
        Soma("W59b-10", "gMono/vCBSMonoReten", "gIBSCBSMono/gMonoReten/vCBSMonoReten"),
        Soma("W59c-10", "gMono/vIBSMonoRet", "gIBSCBSMono/gMonoRet/vIBSMonoRet"),
        Soma("W59d-10", "gMono/vCBSMonoRet", "gIBSCBSMono/gMonoRet/vCBSMonoRet"),
        Soma("W59f-10", "gEstornoCred/vIBSEstCred", "gEstornoCred/vIBSEstCred"),
        Soma("W59g-10", "gEstornoCred/vCBSEstCred", "gEstornoCred/vCBSEstCred"),
    ];

    private static RegraSomaItens Soma(string regra, string total, string campoItem) =>
        new($"{regra}.v1", Total + total, Item + campoItem, grupoExigido: Total);
}

/// <summary>
/// Grupo de total informado sem nenhum item com o tributo (W31-10, W34-10) ou ausente com algum item
/// com o tributo (W31-20, W34-20).
/// </summary>
internal sealed class RegraPresencaTotal(
    string id,
    string grupoTotal,
    string campoTotal,
    string campoItem,
    Func<ItemDocumento, bool> itemComTributo,
    bool informadoSemItem)
    : ImplementacaoRegraBase(id, [campoTotal, campoItem])
{
    protected override AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra)
    {
        if (!GruposReformaPreenchidos(documento))
        {
            return AvaliacaoRegra.NaoAplicavel("Grupos de IBS, CBS e IS não preenchidos no documento.");
        }

        var totalInformado = documento.TemGrupo(grupoTotal + "/");
        var algumItem = documento.Itens.Any(itemComTributo);

        if (informadoSemItem && totalInformado && !algumItem)
        {
            return AvaliacaoRegra.Divergente(new DivergenciaRegra(grupoTotal, "informado", "ausente (nenhum item com o tributo)"));
        }

        if (!informadoSemItem && !totalInformado && algumItem)
        {
            return AvaliacaoRegra.Divergente(new DivergenciaRegra(grupoTotal, "ausente", "informado (há item com o tributo)"));
        }

        return AvaliacaoRegra.Conforme();
    }

    private static bool GruposReformaPreenchidos(LeituraDocumento documento) =>
        documento.TemGrupo("total/ISTot/")
        || documento.TemGrupo("total/IBSCBSTot/")
        || documento.Itens.Any(i => GruposItem.ComIbsCbs(i) || GruposItem.ComIs(i));
}
