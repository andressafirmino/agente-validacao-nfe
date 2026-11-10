namespace AuditoriaNFe.Domain.Regras.ICMSTot;

/// <summary>
/// Regras do grupo ICMSTot de <c>catalogo-regras-totalizadores.md</c> (aprovado em 2026-11-04): uma
/// implementação por regra e versão.
/// </summary>
internal static class RegrasICMSTot
{
    private const string Total = "total/ICMSTot/";

    public static IReadOnlyList<IImplementacaoRegra> Todas { get; } =
    [
        Soma("W03-10", "vBC", "imposto/ICMS/*/vBC"),
        new RegraSomaItens("W04-10.v1", Total + "vICMS", "imposto/ICMS/*/vICMS", GruposItem.CamposIcms,
            item => item.Texto("imposto/ICMS/*/CST") is not ("40" or "41" or "50")),
        Soma("W04a-10", "vICMSDeson", "imposto/ICMS/*/vICMSDeson"),
        Soma("W04b-10", "vFCP", "imposto/ICMS/*/vFCP"),
        Soma("W04c-10", "vFCPUFDest", "imposto/ICMSUFDest/vFCPUFDest"),
        Soma("W04e-10", "vICMSUFDest", "imposto/ICMSUFDest/vICMSUFDest"),
        Soma("W04g-10", "vICMSUFRemet", "imposto/ICMSUFDest/vICMSUFRemet"),
        Soma("W05-10", "vBCST", "imposto/ICMS/*/vBCST"),
        Soma("W06-10", "vST", "imposto/ICMS/*/vICMSST"),
        Soma("W06a-10", "vFCPST", "imposto/ICMS/*/vFCPST"),
        Soma("W06b-10", "vFCPSTRet", "imposto/ICMS/*/vFCPSTRet"),
        Soma("W06b.1-10", "qBCMono", "imposto/ICMS/*/qBCMono"),
        Soma("W06c-10", "vICMSMono", "imposto/ICMS/*/vICMSMono"),
        Soma("W06c.1-10", "qBCMonoReten", "imposto/ICMS/*/qBCMonoReten"),
        Soma("W06d-10", "vICMSMonoReten", "imposto/ICMS/*/vICMSMonoReten"),
        Soma("W06d.1-10", "qBCMonoRet", "imposto/ICMS/*/qBCMonoRet"),
        Soma("W06e-10", "vICMSMonoRet", "imposto/ICMS/*/vICMSMonoRet"),
        new RegraSomaItens("W07-10.v1", Total + "vProd", "prod/vProd", [.. GruposItem.CamposIcms, GruposItem.IndTot],
            item => GruposItem.ComIcms(item) && GruposItem.CompoeTotal(item)),
        new RegraSomaItens("W07-10.v2", Total + "vProd", "prod/vProd",
            [.. GruposItem.CamposIcms, GruposItem.CstIbsCbs, GruposItem.CstIs, GruposItem.IndTot],
            item => (GruposItem.ComIcms(item) || GruposItem.ComIbsCbs(item) || GruposItem.ComIs(item)) && GruposItem.CompoeTotal(item)),
        Soma("W08-10", "vFrete", "prod/vFrete"),
        Soma("W09-10", "vSeg", "prod/vSeg"),
        Soma("W10-10", "vDesc", "prod/vDesc"),
        Soma("W11-10", "vII", "imposto/II/vII"),
        Soma("W12-10", "vIPI", "imposto/IPI/*/vIPI"),
        Soma("W12a-10", "vIPIDevol", "impostoDevol/IPI/vIPIDevol"),
        new RegraSomaItens("W13-10.v1", Total + "vPIS", "imposto/PIS/*/vPIS", GruposItem.CamposIcms, GruposItem.ComIcms),
        new RegraSomaItens("W14-10.v1", Total + "vCOFINS", "imposto/COFINS/*/vCOFINS", GruposItem.CamposIcms, GruposItem.ComIcms),
        Soma("W15-10", "vOutro", "prod/vOutro"),
        new RegraW16(1),
        new RegraW16(2),
        new RegraW16(3),
        new RegraW16(4),
        Soma("W16a-10", "vTotTrib", "imposto/vTotTrib"),
        new RegraW01a10(),
        new RegraW01a20(),
    ];

    private static RegraSomaItens Soma(string regra, string total, string campoItem) =>
        new($"{regra}.v1", Total + total, campoItem);
}

/// <summary>
/// W16-10, valor total da NF-e, nas quatro versões da tabela "Versões da W16-10", com tolerância de
/// R$ 0,50 (*3) e as exceções de veículo novo (tpOp = 2), importação (CFOP iniciado por 3) e
/// vICMSDeson não subtraído.
/// </summary>
internal sealed class RegraW16(int versao) : ImplementacaoRegraBase($"W16-10.v{versao}", CamposLidos)
{
    private const string Total = "total/ICMSTot/";
    private const string Vnf = Total + "vNF";

    private static readonly IReadOnlyList<string> CamposLidos =
    [
        .. new[] { "vProd", "vDesc", "vICMSDeson", "vST", "vFCPST", "vFrete", "vSeg", "vOutro", "vII", "vIPI", "vIPIDevol", "vICMSMonoReten", "vNF" }
            .Select(c => Total + c),
        "total/ISSQNtot/vServ",
        "det[*]/prod/CFOP",
        "det[*]/prod/veicProd/tpOp",
        "det[*]/imposto/PISST/vPIS",
        "det[*]/imposto/PISST/indSomaPISST",
        "det[*]/imposto/COFINSST/vCOFINS",
        "det[*]/imposto/COFINSST/indSomaCOFINSST",
        "det[*]/imposto/ICMS/*/vICMSDeson",
        "det[*]/imposto/ICMS/*/indDeduzDeson",
    ];

    protected override AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra)
    {
        if (documento.Itens.Any(i => i.Texto("prod/CFOP")?.StartsWith('3') == true))
        {
            return AvaliacaoRegra.NaoAplicavel("Importação (CFOP iniciado por 3): a W16-10 não se aplica.");
        }

        decimal T(string campo) => documento.ValorOuZero(Total + campo);
        var veiculoNovo = documento.Itens.Any(i => i.Texto("prod/veicProd/tpOp") == "2");

        var calculado = T("vProd") - T("vDesc") - T("vICMSDeson") + T("vFrete") + T("vSeg") + T("vOutro")
            + T("vII") + T("vIPI") + documento.ValorOuZero("total/ISSQNtot/vServ");

        if (!veiculoNovo)
        {
            calculado += T("vST") + T("vFCPST") + T("vIPIDevol");
        }

        if (versao >= 2 && !(veiculoNovo && versao <= 3))
        {
            calculado += documento.SomaItens("imposto/PISST/vPIS", i => i.Texto("imposto/PISST/indSomaPISST") == "1")
                + documento.SomaItens("imposto/COFINSST/vCOFINS", i => i.Texto("imposto/COFINSST/indSomaCOFINSST") == "1");
        }

        if (versao >= 3)
        {
            calculado += T("vICMSMonoReten");
        }

        var semSubtrairDesoneracao = versao <= 3
            ? calculado + T("vICMSDeson")
            : calculado + documento.SomaItens("imposto/ICMS/*/vICMSDeson", i => i.Texto("imposto/ICMS/*/indDeduzDeson") != "1");

        var informado = documento.ValorOuZero(Vnf);
        var tolerancia = regra.Tolerancia ?? 0m;
        return Math.Abs(informado - semSubtrairDesoneracao) <= tolerancia
            ? AvaliacaoRegra.Conforme()
            : Comparar(Vnf, informado, calculado, regra);
    }
}

/// <summary>W01a-10: vProdLiqTot informado quando algum item tiver vProdLiq (NT 2026.008).</summary>
internal sealed class RegraW01a10() : ImplementacaoRegraBase("W01a-10.v1", ["total/vProdLiqTot", "det[*]/prod/vProdLiq"])
{
    protected override AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra) =>
        documento.Itens.Any(i => i.Texto("prod/vProdLiq") is not null) && !documento.Tem("total/vProdLiqTot")
            ? AvaliacaoRegra.Divergente(new DivergenciaRegra("total/vProdLiqTot", "ausente", "informado"))
            : AvaliacaoRegra.Conforme();
}

/// <summary>W01a-20: vProdLiqTot = Σ itens vProdLiq, só quando vProdLiqTot for informado (NT 2026.008).</summary>
internal sealed class RegraW01a20() : ImplementacaoRegraBase("W01a-20.v1", ["total/vProdLiqTot", "det[*]/prod/vProdLiq"])
{
    protected override AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra) =>
        documento.Valor("total/vProdLiqTot") is { } informado
            ? Comparar("total/vProdLiqTot", informado, documento.SomaItens("prod/vProdLiq"), regra)
            : AvaliacaoRegra.NaoAplicavel("vProdLiqTot não informado.");
}
