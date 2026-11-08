using AuditoriaNFe.Domain.Validacao;
using static AuditoriaNFe.Tests.Dominio.Regras.AplicacaoRegrasTeste;

namespace AuditoriaNFe.Tests.Dominio.Regras;

/// <summary>Regras do grupo ICMSTot do catálogo aprovado (FR-013a, research R-15).</summary>
public sealed class RegrasICMSTotTests
{
    private static readonly DateOnly Data = new(2025, 1, 15);

    /// <summary>Regra, total, campo do item, grupo de ICMS do item e CST.</summary>
    public static TheoryData<string, string, string, string, string> SomasDosItens => new()
    {
        { "W03-10", "ICMSTot/vBC", "imposto/ICMS/ICMS00/vBC", "ICMS00", "00" },
        { "W04a-10", "ICMSTot/vICMSDeson", "imposto/ICMS/ICMS20/vICMSDeson", "ICMS20", "20" },
        { "W04b-10", "ICMSTot/vFCP", "imposto/ICMS/ICMS00/vFCP", "ICMS00", "00" },
        { "W04c-10", "ICMSTot/vFCPUFDest", "imposto/ICMSUFDest/vFCPUFDest", "ICMS00", "00" },
        { "W04e-10", "ICMSTot/vICMSUFDest", "imposto/ICMSUFDest/vICMSUFDest", "ICMS00", "00" },
        { "W04g-10", "ICMSTot/vICMSUFRemet", "imposto/ICMSUFDest/vICMSUFRemet", "ICMS00", "00" },
        { "W05-10", "ICMSTot/vBCST", "imposto/ICMS/ICMS10/vBCST", "ICMS10", "10" },
        { "W06-10", "ICMSTot/vST", "imposto/ICMS/ICMS10/vICMSST", "ICMS10", "10" },
        { "W06a-10", "ICMSTot/vFCPST", "imposto/ICMS/ICMS10/vFCPST", "ICMS10", "10" },
        { "W06b-10", "ICMSTot/vFCPSTRet", "imposto/ICMS/ICMS60/vFCPSTRet", "ICMS60", "60" },
        { "W06c-10", "ICMSTot/vICMSMono", "imposto/ICMS/ICMS02/vICMSMono", "ICMS02", "02" },
        { "W06d-10", "ICMSTot/vICMSMonoReten", "imposto/ICMS/ICMS15/vICMSMonoReten", "ICMS15", "15" },
        { "W06e-10", "ICMSTot/vICMSMonoRet", "imposto/ICMS/ICMS61/vICMSMonoRet", "ICMS61", "61" },
        { "W08-10", "ICMSTot/vFrete", "prod/vFrete", "ICMS00", "00" },
        { "W09-10", "ICMSTot/vSeg", "prod/vSeg", "ICMS00", "00" },
        { "W10-10", "ICMSTot/vDesc", "prod/vDesc", "ICMS00", "00" },
        { "W11-10", "ICMSTot/vII", "imposto/II/vII", "ICMS00", "00" },
        { "W12-10", "ICMSTot/vIPI", "imposto/IPI/IPITrib/vIPI", "ICMS00", "00" },
        { "W12a-10", "ICMSTot/vIPIDevol", "impostoDevol/IPI/vIPIDevol", "ICMS00", "00" },
        { "W15-10", "ICMSTot/vOutro", "prod/vOutro", "ICMS00", "00" },
        { "W16a-10", "ICMSTot/vTotTrib", "imposto/vTotTrib", "ICMS00", "00" },
    };

    /// <summary>Regras monofásicas de quantidade, com tolerância de 0,01 (*4).</summary>
    public static TheoryData<string, string, string, string, string> QuantidadesMonofasicas => new()
    {
        { "W06b.1-10", "ICMSTot/qBCMono", "imposto/ICMS/ICMS02/qBCMono", "ICMS02", "02" },
        { "W06c.1-10", "ICMSTot/qBCMonoReten", "imposto/ICMS/ICMS15/qBCMonoReten", "ICMS15", "15" },
        { "W06d.1-10", "ICMSTot/qBCMonoRet", "imposto/ICMS/ICMS61/qBCMonoRet", "ICMS61", "61" },
    };

    public static TheoryData<string> RegrasMonofasicas =>
        ["W06b.1-10", "W06c-10", "W06c.1-10", "W06d-10", "W06d.1-10", "W06e-10"];

    [Theory]
    [MemberData(nameof(SomasDosItens))]
    public void Total_igual_a_soma_dos_itens_e_conforme(string regraId, string total, string campoItem, string grupo, string cst)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        var resultado = Aplicar(DoisItens(Data, total, 30.50m, campoItem, grupo, cst, 10.00m, 20.50m), regra);

        Conforme(resultado, regra);
    }

    [Theory]
    [MemberData(nameof(SomasDosItens))]
    public void Total_diferente_da_soma_dos_itens_em_um_centavo_e_divergente(
        string regraId, string total, string campoItem, string grupo, string cst)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        var resultado = Aplicar(DoisItens(Data, total, 30.51m, campoItem, grupo, cst, 10.00m, 20.50m), regra);

        Divergente(resultado, regra, "total/" + total, informado: 30.51m, calculado: 30.50m);
    }

    [Theory]
    [MemberData(nameof(SomasDosItens))]
    public void Campo_ausente_no_item_vale_zero(string regraId, string total, string campoItem, string grupo, string cst)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);
        var documento = new DocumentoRegras(Data)
            .Total(total, 10.00m)
            .Item(i => i.Prod("indTot", "1").Icms(grupo, cst).Campo(campoItem, 10.00m))
            .Item(i => i.Prod("indTot", "1").Icms(grupo, cst));

        Conforme(Aplicar(documento, regra), regra);
    }

    [Fact]
    public void W16a_10_total_de_tributos_ausente_vale_zero_e_achado_e_informativo()
    {
        var regra = CatalogoRegrasTeste.Regra("W16a-10");
        var documento = new DocumentoRegras(Data)
            .Item(i => i.Prod("indTot", "1").Icms("ICMS00", "00").Campo("imposto/vTotTrib", 5.00m));

        var achado = Divergente(Aplicar(documento, regra), regra, "total/ICMSTot/vTotTrib", informado: 0m, calculado: 5.00m);

        Assert.Equal(Severidade.Informativa, achado.Severidade);
    }

    [Theory]
    [InlineData("40")]
    [InlineData("41")]
    [InlineData("50")]
    public void W04_10_nao_soma_itens_com_CST_40_41_e_50(string cst)
    {
        var regra = CatalogoRegrasTeste.Regra("W04-10");
        DocumentoRegras Documento(decimal vICMS) => new DocumentoRegras(Data)
            .Total("ICMSTot/vICMS", vICMS)
            .Item(i => i.Prod("indTot", "1").Icms("ICMS00", "00", ("vICMS", 18.00m)))
            .Item(i => i.Prod("indTot", "1").Icms("ICMS40", cst, ("vICMS", 5.00m)));

        Conforme(Aplicar(Documento(18.00m), regra), regra);
        Divergente(Aplicar(Documento(23.00m), regra), regra, "total/ICMSTot/vICMS", informado: 23.00m, calculado: 18.00m);
    }

    [Theory]
    [MemberData(nameof(QuantidadesMonofasicas))]
    public void Quantidade_monofasica_aceita_diferenca_de_ate_um_centesimo(
        string regraId, string total, string campoItem, string grupo, string cst)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);
        Assert.Equal(0.01m, regra.Tolerancia);

        Conforme(Aplicar(DoisItens(Data, total, 150.0000m, campoItem, grupo, cst, 100.0000m, 50.0000m), regra), regra);
        Conforme(Aplicar(DoisItens(Data, total, 150.0100m, campoItem, grupo, cst, 100.0000m, 50.0000m), regra), regra);
        Conforme(Aplicar(DoisItens(Data, total, 149.9900m, campoItem, grupo, cst, 100.0000m, 50.0000m), regra), regra);
    }

    [Theory]
    [MemberData(nameof(QuantidadesMonofasicas))]
    public void Quantidade_monofasica_acima_da_tolerancia_e_divergente(
        string regraId, string total, string campoItem, string grupo, string cst)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        Divergente(
            Aplicar(DoisItens(Data, total, 150.0200m, campoItem, grupo, cst, 100.0000m, 50.0000m), regra),
            regra, "total/" + total, informado: 150.0200m, calculado: 150.0000m);
        Divergente(
            Aplicar(DoisItens(Data, total, 149.9800m, campoItem, grupo, cst, 100.0000m, 50.0000m), regra),
            regra, "total/" + total, informado: 149.9800m, calculado: 150.0000m);
    }

    [Theory]
    [MemberData(nameof(RegrasMonofasicas))]
    public void Regras_monofasicas_nao_se_aplicam_antes_de_30_10_2023(string regraId)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);
        var total = regraId switch
        {
            "W06b.1-10" => "ICMSTot/qBCMono",
            "W06c-10" => "ICMSTot/vICMSMono",
            "W06c.1-10" => "ICMSTot/qBCMonoReten",
            "W06d-10" => "ICMSTot/vICMSMonoReten",
            "W06d.1-10" => "ICMSTot/qBCMonoRet",
            _ => "ICMSTot/vICMSMonoRet",
        };
        var documento = new DocumentoRegras(new DateOnly(2023, 10, 29)).Total(total, 999.00m);

        NaoAvaliada(Aplicar(documento, regra));
        Assert.Equal(new DateOnly(2023, 10, 30), regra.VigenciaInicio);
    }

    [Fact]
    public void W07_10_v1_soma_so_itens_com_ICMS_e_indTot_1()
    {
        var versoes = CatalogoRegrasTeste.Versoes("W07-10");
        var v1 = CatalogoRegrasTeste.Regra("W07-10", 1);
        DocumentoRegras Documento(decimal vProd) => new DocumentoRegras(new DateOnly(2026, 8, 2))
            .Total("ICMSTot/vProd", vProd)
            .Item(i => i.Prod("vProd", 100.00m).Prod("indTot", "1").Icms("ICMS00", "00"))
            .Item(i => i.Prod("vProd", 50.00m).Prod("indTot", "0").Icms("ICMS00", "00"))
            .Item(i => i.Prod("vProd", 30.00m).Prod("indTot", "1").Issqn(("vBC", 30.00m)));

        Conforme(Aplicar(Documento(100.00m), versoes), v1);
        Divergente(Aplicar(Documento(180.00m), versoes), v1, "total/ICMSTot/vProd", informado: 180.00m, calculado: 100.00m);
    }

    [Fact]
    public void W07_10_v2_soma_tambem_itens_sujeitos_ao_IBS_CBS_ou_IS()
    {
        var versoes = CatalogoRegrasTeste.Versoes("W07-10");
        DocumentoRegras Documento(DateOnly data) => new DocumentoRegras(data)
            .Total("ICMSTot/vProd", 140.00m)
            .Item(i => i.Prod("vProd", 100.00m).Prod("indTot", "1").Icms("ICMS00", "00"))
            .Item(i => i.Prod("vProd", 40.00m).Prod("indTot", "1").IbsCbs("gIBSCBS/vBC", 40.00m).Is(1.00m));

        Conforme(Aplicar(Documento(new DateOnly(2026, 8, 3)), versoes), CatalogoRegrasTeste.Regra("W07-10", 2));
        Divergente(
            Aplicar(Documento(new DateOnly(2026, 8, 2)), versoes),
            CatalogoRegrasTeste.Regra("W07-10", 1), "total/ICMSTot/vProd", informado: 140.00m, calculado: 100.00m);
    }

    [Fact]
    public void W07_10_v2_e_divergente_quando_o_total_nao_fecha()
    {
        var v2 = CatalogoRegrasTeste.Regra("W07-10", 2);
        var documento = new DocumentoRegras(new DateOnly(2026, 8, 3))
            .Total("ICMSTot/vProd", 100.01m)
            .Item(i => i.Prod("vProd", 100.00m).Prod("indTot", "1").Icms("ICMS00", "00"));

        Divergente(Aplicar(documento, CatalogoRegrasTeste.Versoes("W07-10")), v2, "total/ICMSTot/vProd", 100.01m, 100.00m);
    }

    [Theory]
    [InlineData("W13-10", "ICMSTot/vPIS", "imposto/PIS/PISAliq/vPIS")]
    [InlineData("W14-10", "ICMSTot/vCOFINS", "imposto/COFINS/COFINSAliq/vCOFINS")]
    public void PIS_e_COFINS_somam_so_itens_com_grupo_ICMS(string regraId, string total, string campoItem)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);
        DocumentoRegras Documento(decimal valor) => new DocumentoRegras(Data)
            .Total(total, valor)
            .Item(i => i.Prod("indTot", "1").Icms("ICMS00", "00").Campo(campoItem, 6.50m))
            .Item(i => i.Prod("indTot", "1").Issqn(("vBC", 100.00m)).Campo(campoItem, 3.00m));

        Conforme(Aplicar(Documento(6.50m), regra), regra);
        Divergente(Aplicar(Documento(9.50m), regra), regra, "total/" + total, informado: 9.50m, calculado: 6.50m);
    }

    [Fact]
    public void W01a_10_exige_vProdLiqTot_quando_algum_item_tem_vProdLiq()
    {
        var regra = CatalogoRegrasTeste.Regra("W01a-10");
        var comItem = new DocumentoRegras(new DateOnly(2026, 11, 16))
            .Item(i => i.Prod("vProd", 100.00m).Prod("vProdLiq", 90.00m).Prod("indTot", "1").Icms("ICMS00", "00"));
        var semItem = new DocumentoRegras(new DateOnly(2026, 11, 16))
            .Item(i => i.Prod("vProd", 100.00m).Prod("indTot", "1").Icms("ICMS00", "00"));

        Divergente(Aplicar(comItem, regra), regra, "total/vProdLiqTot");
        Conforme(Aplicar(semItem, regra), regra);
    }

    [Fact]
    public void W01a_20_vProdLiqTot_informado_e_a_soma_dos_vProdLiq()
    {
        var regra = CatalogoRegrasTeste.Regra("W01a-20");
        DocumentoRegras Documento(decimal? total)
        {
            var documento = new DocumentoRegras(new DateOnly(2026, 11, 16))
                .Item(i => i.Prod("vProdLiq", 50.00m).Prod("indTot", "1").Icms("ICMS00", "00"))
                .Item(i => i.Prod("vProdLiq", 40.00m).Prod("indTot", "1").Icms("ICMS00", "00"));
            return total is null ? documento : documento.Campo("total/vProdLiqTot", total.Value);
        }

        Conforme(Aplicar(Documento(90.00m), regra), regra);
        Divergente(Aplicar(Documento(91.00m), regra), regra, "total/vProdLiqTot", informado: 91.00m, calculado: 90.00m);
        NaoAplicavel(Aplicar(Documento(null), regra), regra);
    }

    [Theory]
    [InlineData("W01a-10")]
    [InlineData("W01a-20")]
    public void Regras_do_valor_liquido_nao_se_aplicam_antes_de_16_11_2026(string regraId)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);
        var documento = new DocumentoRegras(new DateOnly(2026, 11, 15))
            .Campo("total/vProdLiqTot", 1.00m)
            .Item(i => i.Prod("vProdLiq", 90.00m).Prod("indTot", "1").Icms("ICMS00", "00"));

        NaoAvaliada(Aplicar(documento, regra));
    }

    [Fact]
    public void Regra_nao_e_aplicada_antes_do_inicio_da_vigencia()
    {
        var documento = DoisItens(new DateOnly(2017, 12, 3), "ICMSTot/vBC", 1.00m, "imposto/ICMS/ICMS00/vBC", "ICMS00", "00", 10m, 20m);

        NaoAvaliada(Aplicar(documento, CatalogoRegrasTeste.Regra("W03-10")));
    }

    // W16-10: vProd 1000 − vDesc 10 + vST 20 + vFCPST 2 + vFrete 30 + vSeg 5 + vOutro 3 + vIPI 50 = 1100.

    public static TheoryData<int, string, decimal, bool> W16PorVersao
    {
        get
        {
            var dados = new TheoryData<int, string, decimal, bool>();
            foreach (var (versao, data) in new[] { (1, "2020-01-15"), (2, "2022-01-15"), (3, "2023-06-15"), (4, "2025-01-15") })
            {
                dados.Add(versao, data, 0m, true);
                dados.Add(versao, data, 0.50m, true);
                dados.Add(versao, data, -0.50m, true);
                dados.Add(versao, data, 0.51m, false);
                dados.Add(versao, data, -0.51m, false);
            }

            return dados;
        }
    }

    [Theory]
    [MemberData(nameof(W16PorVersao))]
    public void W16_10_aceita_ate_50_centavos_para_mais_ou_para_menos(int versao, string data, decimal diferenca, bool conforme)
    {
        var regra = CatalogoRegrasTeste.Regra("W16-10", versao);
        Assert.Equal(0.50m, regra.Tolerancia);

        var resultado = Aplicar(W16(DateOnly.Parse(data), 1100.00m + diferenca), CatalogoRegrasTeste.Versoes("W16-10"));

        if (conforme)
        {
            Conforme(resultado, regra);
        }
        else
        {
            Divergente(resultado, regra, "total/ICMSTot/vNF", informado: 1100.00m + diferenca, calculado: 1100.00m);
        }
    }

    [Theory]
    [InlineData("2021-10-03", 1)]
    [InlineData("2021-10-04", 2)]
    [InlineData("2023-03-29", 2)]
    [InlineData("2023-03-30", 3)]
    [InlineData("2024-06-30", 3)]
    [InlineData("2024-07-01", 4)]
    public void W16_10_aplica_a_versao_vigente_na_data_de_referencia(string data, int versao)
    {
        var aplicada = Assert.Single(Aplicar(W16(DateOnly.Parse(data), 1100.00m), CatalogoRegrasTeste.Versoes("W16-10")).RegrasAplicadas);

        Assert.Equal(("W16-10", versao), (aplicada.RegraId, aplicada.VersaoRegra));
    }

    [Fact]
    public void W16_10_nao_e_aplicada_antes_da_primeira_versao()
    {
        NaoAvaliada(Aplicar(W16(new DateOnly(2017, 12, 3), 5000.00m), CatalogoRegrasTeste.Versoes("W16-10")));
    }

    [Fact]
    public void W16_10_soma_o_vServ_do_ISSQNtot()
    {
        var regra = CatalogoRegrasTeste.Regra("W16-10", 4);

        var resultado = Aplicar(W16(Data, 1300.00m, d => d.Total("ISSQNtot/vServ", 200.00m)), CatalogoRegrasTeste.Versoes("W16-10"));

        Conforme(resultado, regra);
    }

    [Fact]
    public void W16_10_v2_soma_PIS_ST_e_COFINS_ST_quando_indicado()
    {
        var versoes = CatalogoRegrasTeste.Versoes("W16-10");
        DocumentoRegras Documento(DateOnly data, decimal vNF, string indicador) => W16(data, vNF, item: i => i
            .Campo("imposto/PISST/vPIS", 7.00m).Campo("imposto/PISST/indSomaPISST", indicador)
            .Campo("imposto/COFINSST/vCOFINS", 3.00m).Campo("imposto/COFINSST/indSomaCOFINSST", indicador));

        Conforme(Aplicar(Documento(new DateOnly(2021, 10, 4), 1110.00m, "1"), versoes), CatalogoRegrasTeste.Regra("W16-10", 2));
        Divergente(
            Aplicar(Documento(new DateOnly(2021, 10, 3), 1110.00m, "1"), versoes),
            CatalogoRegrasTeste.Regra("W16-10", 1), "total/ICMSTot/vNF", informado: 1110.00m, calculado: 1100.00m);
        Conforme(Aplicar(Documento(new DateOnly(2021, 10, 4), 1100.00m, "0"), versoes), CatalogoRegrasTeste.Regra("W16-10", 2));
        Divergente(
            Aplicar(Documento(new DateOnly(2021, 10, 4), 1110.00m, "0"), versoes),
            CatalogoRegrasTeste.Regra("W16-10", 2), "total/ICMSTot/vNF", informado: 1110.00m, calculado: 1100.00m);
    }

    [Theory]
    [InlineData("2023-03-30", 3)]
    [InlineData("2025-01-15", 4)]
    public void W16_10_v3_e_v4_somam_o_vICMSMonoReten(string data, int versao)
    {
        var resultado = Aplicar(
            W16(DateOnly.Parse(data), 1115.00m, d => d.Total("ICMSTot/vICMSMonoReten", 15.00m)),
            CatalogoRegrasTeste.Versoes("W16-10"));

        Conforme(resultado, CatalogoRegrasTeste.Regra("W16-10", versao));
    }

    [Fact]
    public void W16_10_v2_nao_soma_o_vICMSMonoReten()
    {
        var resultado = Aplicar(
            W16(new DateOnly(2023, 3, 29), 1115.00m, d => d.Total("ICMSTot/vICMSMonoReten", 15.00m)),
            CatalogoRegrasTeste.Versoes("W16-10"));

        Divergente(resultado, CatalogoRegrasTeste.Regra("W16-10", 2), "total/ICMSTot/vNF", informado: 1115.00m, calculado: 1100.00m);
    }

    [Theory]
    [InlineData("2020-01-15", 1)]
    [InlineData("2022-01-15", 2)]
    [InlineData("2023-06-15", 3)]
    [InlineData("2025-01-15", 4)]
    public void W16_10_veiculo_novo_tpOp_2_exclui_vST_vFCPST_e_vIPIDevol(string data, int versao)
    {
        var regra = CatalogoRegrasTeste.Regra("W16-10", versao);
        var versoes = CatalogoRegrasTeste.Versoes("W16-10");
        DocumentoRegras Documento(decimal vNF, bool veiculoNovo) => W16(
            DateOnly.Parse(data), vNF, d => d.Total("ICMSTot/vIPIDevol", 4.00m),
            i => { if (veiculoNovo) { i.Prod("veicProd/tpOp", "2"); } });

        Conforme(Aplicar(Documento(1078.00m, veiculoNovo: true), versoes), regra);
        Conforme(Aplicar(Documento(1104.00m, veiculoNovo: false), versoes), regra);
        Divergente(Aplicar(Documento(1078.00m, veiculoNovo: false), versoes), regra, "total/ICMSTot/vNF", informado: 1078.00m, calculado: 1104.00m);
    }

    [Theory]
    [InlineData("2020-01-15", 1)]
    [InlineData("2022-01-15", 2)]
    [InlineData("2023-06-15", 3)]
    [InlineData("2025-01-15", 4)]
    public void W16_10_nao_se_aplica_a_importacao_CFOP_iniciado_por_3(string data, int versao)
    {
        var resultado = Aplicar(W16(DateOnly.Parse(data), 5000.00m, cfop: "3102"), CatalogoRegrasTeste.Versoes("W16-10"));

        NaoAplicavel(resultado, CatalogoRegrasTeste.Regra("W16-10", versao));
    }

    [Theory]
    [InlineData("2020-01-15", 1)]
    [InlineData("2022-01-15", 2)]
    [InlineData("2023-06-15", 3)]
    public void W16_10_v1_a_v3_nao_rejeitam_vICMSDeson_nao_subtraido(string data, int versao)
    {
        var regra = CatalogoRegrasTeste.Regra("W16-10", versao);
        var versoes = CatalogoRegrasTeste.Versoes("W16-10");

        Conforme(Aplicar(ComDesoneracao(DateOnly.Parse(data), 1060.00m, "1"), versoes), regra);
        Conforme(Aplicar(ComDesoneracao(DateOnly.Parse(data), 1100.00m, "1"), versoes), regra);
    }

    [Fact]
    public void W16_10_v4_so_aceita_vICMSDeson_nao_subtraido_dos_itens_sem_indDeduzDeson_1()
    {
        var regra = CatalogoRegrasTeste.Regra("W16-10", 4);
        var versoes = CatalogoRegrasTeste.Versoes("W16-10");

        Conforme(Aplicar(ComDesoneracao(Data, 1060.00m, "1"), versoes), regra);
        Divergente(Aplicar(ComDesoneracao(Data, 1100.00m, "1"), versoes), regra, "total/ICMSTot/vNF", informado: 1100.00m, calculado: 1060.00m);
        Conforme(Aplicar(ComDesoneracao(Data, 1100.00m, "0"), versoes), regra);
        Conforme(Aplicar(ComDesoneracao(Data, 1100.00m, null), versoes), regra);
    }

    private static DocumentoRegras DoisItens(
        DateOnly data, string total, decimal valorTotal, string campoItem, string grupo, string cst, decimal item1, decimal item2) =>
        new DocumentoRegras(data)
            .Total(total, valorTotal)
            .Item(i => i.Prod("indTot", "1").Icms(grupo, cst).Campo(campoItem, item1))
            .Item(i => i.Prod("indTot", "1").Icms(grupo, cst).Campo(campoItem, item2));

    private static DocumentoRegras W16(
        DateOnly data,
        decimal vNF,
        Action<DocumentoRegras>? totais = null,
        Action<ItemRegras>? item = null,
        string cfop = "5102")
    {
        var documento = new DocumentoRegras(data)
            .Total("ICMSTot/vProd", 1000.00m)
            .Total("ICMSTot/vDesc", 10.00m)
            .Total("ICMSTot/vST", 20.00m)
            .Total("ICMSTot/vFCPST", 2.00m)
            .Total("ICMSTot/vFrete", 30.00m)
            .Total("ICMSTot/vSeg", 5.00m)
            .Total("ICMSTot/vOutro", 3.00m)
            .Total("ICMSTot/vIPI", 50.00m)
            .Total("ICMSTot/vNF", vNF)
            .Item(i =>
            {
                i.Prod("CFOP", cfop).Prod("vProd", 1000.00m).Prod("indTot", "1").Icms("ICMS10", "10");
                item?.Invoke(i);
            });
        totais?.Invoke(documento);
        return documento;
    }

    private static DocumentoRegras ComDesoneracao(DateOnly data, decimal vNF, string? indDeduzDeson) => W16(
        data,
        vNF,
        d => d.Total("ICMSTot/vICMSDeson", 40.00m).Item(i =>
        {
            i.Prod("CFOP", "5102").Prod("indTot", "1").Icms("ICMS20", "20", ("vICMSDeson", 40.00m));
            if (indDeduzDeson is not null)
            {
                i.Campo("imposto/ICMS/ICMS20/indDeduzDeson", indDeduzDeson);
            }
        }));
}
