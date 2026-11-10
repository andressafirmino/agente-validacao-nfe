using static AuditoriaNFe.Tests.Dominio.Regras.AplicacaoRegrasTeste;

namespace AuditoriaNFe.Tests.Dominio.Regras;

/// <summary>
/// Regras de IBS, CBS e IS do catálogo aprovado (NT 2025.002 v1.53), aplicadas só com os grupos
/// preenchidos. Caminhos conforme <c>DFeTiposBasicos_v1.00.xsd</c> do pacote PL_010g_v1.03.
/// </summary>
public sealed class RegrasIbsCbsIsTests
{
    private static readonly DateOnly Data = new(2026, 3, 2);

    /// <summary>Regra, total em <c>IBSCBSTot</c> e campo do item em <c>imposto/IBSCBS</c>.</summary>
    private static readonly (string Regra, string Total, string CampoItem)[] RegrasDeSoma =
    [
        ("W35-10", "vBCIBSCBS", "gIBSCBS/vBC"),
        ("W38-10", "gIBS/gIBSUF/vDif", "gIBSCBS/gIBSUF/gDif/vDif"),
        ("W39-10", "gIBS/gIBSUF/vDevTrib", "gIBSCBS/gIBSUF/gDevTrib/vDevTrib"),
        ("W41-10", "gIBS/gIBSUF/vIBSUF", "gIBSCBS/gIBSUF/vIBSUF"),
        ("W43-10", "gIBS/gIBSMun/vDif", "gIBSCBS/gIBSMun/gDif/vDif"),
        ("W44-10", "gIBS/gIBSMun/vDevTrib", "gIBSCBS/gIBSMun/gDevTrib/vDevTrib"),
        ("W46-10", "gIBS/gIBSMun/vIBSMun", "gIBSCBS/gIBSMun/vIBSMun"),
        ("W47-10", "gIBS/vIBS", "gIBSCBS/vIBS"),
        ("W48-10", "gIBS/vCredPres", "gCredPresOper/gIBSCredPres/vCredPres"),
        ("W53-10", "gCBS/vDif", "gIBSCBS/gCBS/gDif/vDif"),
        ("W54-10", "gCBS/vDevTrib", "gIBSCBS/gCBS/gDevTrib/vDevTrib"),
        ("W56-10", "gCBS/vCBS", "gIBSCBS/gCBS/vCBS"),
        ("W56a-10", "gCBS/vCredPres", "gCredPresOper/gCBSCredPres/vCredPres"),
        ("W58-10", "gMono/vIBSMono", "gIBSCBSMono/gMonoPadrao/vIBSMono"),
        ("W59-10", "gMono/vCBSMono", "gIBSCBSMono/gMonoPadrao/vCBSMono"),
        ("W59a-10", "gMono/vIBSMonoReten", "gIBSCBSMono/gMonoReten/vIBSMonoReten"),
        ("W59b-10", "gMono/vCBSMonoReten", "gIBSCBSMono/gMonoReten/vCBSMonoReten"),
        ("W59c-10", "gMono/vIBSMonoRet", "gIBSCBSMono/gMonoRet/vIBSMonoRet"),
        ("W59d-10", "gMono/vCBSMonoRet", "gIBSCBSMono/gMonoRet/vCBSMonoRet"),
        ("W59f-10", "gEstornoCred/vIBSEstCred", "gEstornoCred/vIBSEstCred"),
        ("W59g-10", "gEstornoCred/vCBSEstCred", "gEstornoCred/vCBSEstCred"),
    ];

    public static TheoryData<string, string, string> Somas
    {
        get
        {
            var dados = new TheoryData<string, string, string>();
            foreach (var (regra, total, campoItem) in RegrasDeSoma)
            {
                dados.Add(regra, total, campoItem);
            }

            return dados;
        }
    }

    public static TheoryData<string> TodasAsRegras =>
        ["W31-10", "W31-20", "W34-10", "W34-20", .. RegrasDeSoma.Select(r => r.Regra)];

    [Theory]
    [MemberData(nameof(Somas))]
    public void Total_igual_a_soma_dos_itens_e_conforme(string regraId, string total, string campoItem)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        Conforme(Aplicar(DoisItens(Data, total, 75.25m, campoItem), regra), regra);
    }

    [Theory]
    [MemberData(nameof(Somas))]
    public void Total_diferente_da_soma_dos_itens_e_divergente(string regraId, string total, string campoItem)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        Divergente(
            Aplicar(DoisItens(Data, total, 75.24m, campoItem), regra),
            regra, "total/IBSCBSTot/" + total, informado: 75.24m, calculado: 75.25m);
    }

    [Theory]
    [MemberData(nameof(TodasAsRegras))]
    public void Sem_os_grupos_de_IBS_CBS_e_IS_a_regra_nao_e_aplicavel(string regraId)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);
        var documento = new DocumentoRegras(Data)
            .Total("ICMSTot/vProd", 100.00m)
            .Item(i => i.Prod("vProd", 100.00m).Prod("indTot", "1").Icms("ICMS00", "00"));

        NaoAplicavel(Aplicar(documento, regra), regra);
    }

    [Theory]
    [MemberData(nameof(Somas))]
    public void Regra_nao_e_aplicada_antes_do_inicio_da_vigencia(string regraId, string total, string campoItem)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        NaoAvaliada(Aplicar(DoisItens(regra.VigenciaInicio.AddDays(-1), total, 1.00m, campoItem), regra));
    }

    [Theory]
    [InlineData("W59f-10")]
    [InlineData("W59g-10")]
    public void Estorno_de_credito_vale_desde_02_02_2026(string regraId)
    {
        Assert.Equal(new DateOnly(2026, 2, 2), CatalogoRegrasTeste.Regra(regraId).VigenciaInicio);
    }

    [Fact]
    public void W31_10_ISTot_informado_sem_item_com_IS_e_divergente()
    {
        var regra = CatalogoRegrasTeste.Regra("W31-10");
        var comIs = new DocumentoRegras(Data).Total("ISTot/vIS", 2.00m).Item(i => i.Prod("indTot", "1").Is(2.00m));
        var semIs = new DocumentoRegras(Data).Total("ISTot/vIS", 2.00m).Item(i => i.Prod("indTot", "1").Icms("ICMS00", "00"));

        Conforme(Aplicar(comIs, regra), regra);
        Divergente(Aplicar(semIs, regra), regra, "total/ISTot");
    }

    [Fact]
    public void W31_20_ISTot_ausente_com_item_com_IS_e_divergente()
    {
        var regra = CatalogoRegrasTeste.Regra("W31-20");
        var comTotal = new DocumentoRegras(Data).Total("ISTot/vIS", 2.00m).Item(i => i.Prod("indTot", "1").Is(2.00m));
        var semTotal = new DocumentoRegras(Data).Item(i => i.Prod("indTot", "1").Is(2.00m));

        Conforme(Aplicar(comTotal, regra), regra);
        Divergente(Aplicar(semTotal, regra), regra, "total/ISTot");
    }

    [Fact]
    public void W34_10_IBSCBSTot_informado_sem_item_com_IBSCBS_e_divergente()
    {
        var regra = CatalogoRegrasTeste.Regra("W34-10");
        var comItem = new DocumentoRegras(Data)
            .Total("IBSCBSTot/vBCIBSCBS", 100.00m)
            .Item(i => i.Prod("indTot", "1").IbsCbs("gIBSCBS/vBC", 100.00m));
        var semItem = new DocumentoRegras(Data)
            .Total("IBSCBSTot/vBCIBSCBS", 100.00m)
            .Item(i => i.Prod("indTot", "1").Icms("ICMS00", "00"));

        Conforme(Aplicar(comItem, regra), regra);
        Divergente(Aplicar(semItem, regra), regra, "total/IBSCBSTot");
    }

    [Fact]
    public void W34_20_IBSCBSTot_ausente_com_item_com_IBSCBS_e_divergente()
    {
        var regra = CatalogoRegrasTeste.Regra("W34-20");
        var comTotal = new DocumentoRegras(Data)
            .Total("IBSCBSTot/vBCIBSCBS", 100.00m)
            .Item(i => i.Prod("indTot", "1").IbsCbs("gIBSCBS/vBC", 100.00m));
        var semTotal = new DocumentoRegras(Data)
            .Item(i => i.Prod("indTot", "1").IbsCbs("gIBSCBS/vBC", 100.00m));

        Conforme(Aplicar(comTotal, regra), regra);
        Divergente(Aplicar(semTotal, regra), regra, "total/IBSCBSTot");
    }

    /// <summary>Total em <c>IBSCBSTot</c> e dois itens com o campo (50,00 + 25,25).</summary>
    private static DocumentoRegras DoisItens(DateOnly data, string total, decimal valorTotal, string campoItem) =>
        new DocumentoRegras(data)
            .Total("IBSCBSTot/" + total, valorTotal)
            .Item(i => i.Prod("indTot", "1").Icms("ICMS00", "00").IbsCbs(campoItem, 50.00m))
            .Item(i => i.Prod("indTot", "1").Icms("ICMS00", "00").IbsCbs(campoItem, 25.25m));
}
