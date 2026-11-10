namespace AuditoriaNFe.Domain.Regras.ISSQNtot;

/// <summary>Regras do grupo ISSQNtot do catálogo aprovado: totais iguais à soma dos itens sujeitos ao ISSQN.</summary>
internal static class RegrasISSQNtot
{
    private const string Total = "total/ISSQNtot/";

    public static IReadOnlyList<IImplementacaoRegra> Todas { get; } =
    [
        Soma("W18-10", "vServ", "prod/vProd"),
        Soma("W19-10", "vBC", "imposto/ISSQN/vBC"),
        Soma("W20-10", "vISS", "imposto/ISSQN/vISSQN"),
        Soma("W21-10", "vPIS", "imposto/PIS/*/vPIS"),
        Soma("W22-10", "vCOFINS", "imposto/COFINS/*/vCOFINS"),
        Soma("W22b-10", "vDeducao", "imposto/ISSQN/vDeducao"),
        Soma("W22c-10", "vOutro", "imposto/ISSQN/vOutro"),
        Soma("W22d-10", "vDescIncond", "imposto/ISSQN/vDescIncond"),
        Soma("W22e-10", "vDescCond", "imposto/ISSQN/vDescCond"),
        Soma("W22f-10", "vISSRet", "imposto/ISSQN/vISSRet"),
    ];

    private static RegraSomaItens Soma(string regra, string total, string campoItem) =>
        new($"{regra}.v1", Total + total, campoItem, [GruposItem.ServicoIssqn], GruposItem.ComIssqn, Total);
}
