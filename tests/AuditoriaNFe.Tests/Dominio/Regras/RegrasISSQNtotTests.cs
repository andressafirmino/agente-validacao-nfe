using static AuditoriaNFe.Tests.Dominio.Regras.AplicacaoRegrasTeste;

namespace AuditoriaNFe.Tests.Dominio.Regras;

/// <summary>Regras do grupo ISSQNtot do catálogo aprovado.</summary>
public sealed class RegrasISSQNtotTests
{
    private static readonly DateOnly Data = new(2025, 1, 15);

    /// <summary>Regra, total e campo do item sujeito ao ISSQN que é somado.</summary>
    public static TheoryData<string, string, string> Regras => new()
    {
        { "W18-10", "ISSQNtot/vServ", "prod/vProd" },
        { "W19-10", "ISSQNtot/vBC", "imposto/ISSQN/vBC" },
        { "W20-10", "ISSQNtot/vISS", "imposto/ISSQN/vISSQN" },
        { "W21-10", "ISSQNtot/vPIS", "imposto/PIS/PISAliq/vPIS" },
        { "W22-10", "ISSQNtot/vCOFINS", "imposto/COFINS/COFINSAliq/vCOFINS" },
        { "W22b-10", "ISSQNtot/vDeducao", "imposto/ISSQN/vDeducao" },
        { "W22c-10", "ISSQNtot/vOutro", "imposto/ISSQN/vOutro" },
        { "W22d-10", "ISSQNtot/vDescIncond", "imposto/ISSQN/vDescIncond" },
        { "W22e-10", "ISSQNtot/vDescCond", "imposto/ISSQN/vDescCond" },
        { "W22f-10", "ISSQNtot/vISSRet", "imposto/ISSQN/vISSRet" },
    };

    [Theory]
    [MemberData(nameof(Regras))]
    public void Total_igual_a_soma_dos_itens_sujeitos_ao_ISSQN_e_conforme(string regraId, string total, string campoItem)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        Conforme(Aplicar(Documento(total, 300.00m, campoItem), regra), regra);
    }

    [Theory]
    [MemberData(nameof(Regras))]
    public void Total_diferente_da_soma_dos_itens_sujeitos_ao_ISSQN_e_divergente(string regraId, string total, string campoItem)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        Divergente(Aplicar(Documento(total, 300.01m, campoItem), regra), regra, "total/" + total, informado: 300.01m, calculado: 300.00m);
    }

    [Theory]
    [MemberData(nameof(Regras))]
    public void Sem_o_grupo_ISSQNtot_a_regra_nao_e_aplicavel(string regraId, string total, string campoItem)
    {
        _ = total;
        _ = campoItem;
        var regra = CatalogoRegrasTeste.Regra(regraId);
        var documento = new DocumentoRegras(Data)
            .Total("ICMSTot/vProd", 100.00m)
            .Item(i => i.Prod("vProd", 100.00m).Prod("indTot", "1").Icms("ICMS00", "00"));

        NaoAplicavel(Aplicar(documento, regra), regra);
    }

    [Theory]
    [MemberData(nameof(Regras))]
    public void Regra_nao_e_aplicada_antes_do_inicio_da_vigencia(string regraId, string total, string campoItem)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        NaoAvaliada(Aplicar(Documento(total, 999.00m, campoItem, new DateOnly(2017, 12, 3)), regra));
    }

    /// <summary>
    /// Dois itens com ISSQN (100 + 200) e um item com ICMS, que não entra na soma; o item com ICMS
    /// só recebe o campo quando ele não é do grupo ISSQN (valor 50).
    /// </summary>
    private static DocumentoRegras Documento(string total, decimal valorTotal, string campoItem, DateOnly? data = null) =>
        new DocumentoRegras(data ?? Data)
            .Total(total, valorTotal)
            .Item(i => i.Prod("indTot", "1").Issqn().Campo(campoItem, 100.00m))
            .Item(i => i.Prod("indTot", "1").Issqn().Campo(campoItem, 200.00m))
            .Item(i =>
            {
                i.Prod("indTot", "1").Icms("ICMS00", "00");
                if (!campoItem.StartsWith("imposto/ISSQN/", StringComparison.Ordinal))
                {
                    i.Campo(campoItem, 50.00m);
                }
            });
}
