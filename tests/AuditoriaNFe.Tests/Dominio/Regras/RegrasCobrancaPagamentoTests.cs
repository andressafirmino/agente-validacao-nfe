using static AuditoriaNFe.Tests.Dominio.Regras.AplicacaoRegrasTeste;

namespace AuditoriaNFe.Tests.Dominio.Regras;

/// <summary>Regras dos grupos Y (cobrança) e YA (pagamento) do catálogo aprovado.</summary>
public sealed class RegrasCobrancaPagamentoTests
{
    private static readonly DateOnly Data = new(2025, 1, 15);

    [Theory]
    [InlineData(10.00)]
    [InlineData(100.00)]
    public void Y05_10_desconto_ate_o_valor_original_e_conforme(decimal vDesc)
    {
        var regra = CatalogoRegrasTeste.Regra("Y05-10");

        Conforme(Aplicar(Fatura(Data, vOrig: 100.00m, vDesc: vDesc), regra), regra);
    }

    [Fact]
    public void Y05_10_desconto_maior_que_o_valor_original_e_divergente()
    {
        var regra = CatalogoRegrasTeste.Regra("Y05-10");

        Divergente(Aplicar(Fatura(Data, vOrig: 100.00m, vDesc: 100.01m), regra), regra, "cobr/fat/vDesc", informado: 100.01m);
    }

    [Fact]
    public void Y05_10_desconto_nao_informado_vale_zero()
    {
        var regra = CatalogoRegrasTeste.Regra("Y05-10");

        Conforme(Aplicar(Fatura(Data, vOrig: 100.00m), regra), regra);
    }

    [Fact]
    public void Y06_10_valor_liquido_e_o_original_menos_o_desconto()
    {
        var regra = CatalogoRegrasTeste.Regra("Y06-10");

        Conforme(Aplicar(Fatura(Data, vOrig: 100.00m, vDesc: 10.00m, vLiq: 90.00m), regra), regra);
        Conforme(Aplicar(Fatura(Data, vOrig: 100.00m, vLiq: 100.00m), regra), regra);
        Divergente(
            Aplicar(Fatura(Data, vOrig: 100.00m, vDesc: 10.00m, vLiq: 95.00m), regra),
            regra, "cobr/fat/vLiq", informado: 95.00m, calculado: 90.00m);
    }

    [Fact]
    public void Y06_10_so_se_aplica_com_vLiq_e_vOrig_informados()
    {
        var regra = CatalogoRegrasTeste.Regra("Y06-10");

        NaoAplicavel(Aplicar(Fatura(Data, vOrig: 100.00m, vDesc: 10.00m), regra), regra);
        NaoAplicavel(Aplicar(Fatura(Data, vDesc: 10.00m, vLiq: 95.00m), regra), regra);
    }

    [Fact]
    public void Y10_10_soma_das_duplicatas_e_o_valor_liquido()
    {
        var regra = CatalogoRegrasTeste.Regra("Y10-10");

        Conforme(Aplicar(Fatura(Data, vOrig: 90.00m, vLiq: 90.00m, duplicatas: [60.00m, 30.00m]), regra), regra);
        Divergente(
            Aplicar(Fatura(Data, vOrig: 90.00m, vLiq: 90.00m, duplicatas: [60.00m, 31.00m]), regra),
            regra, "cobr/fat/vLiq", informado: 90.00m, calculado: 91.00m);
    }

    [Fact]
    public void Y10_10_nao_e_aplicada_antes_de_02_07_2018_e_Y05_10_ja_e()
    {
        var documento = Fatura(new DateOnly(2018, 6, 10), vOrig: 90.00m, vDesc: 95.00m, vLiq: 90.00m, duplicatas: [10.00m]);

        NaoAvaliada(Aplicar(documento, CatalogoRegrasTeste.Regra("Y10-10")));
        Divergente(Aplicar(documento, CatalogoRegrasTeste.Regra("Y05-10")), CatalogoRegrasTeste.Regra("Y05-10"), "cobr/fat/vDesc");
    }

    [Theory]
    [InlineData("Y05-10")]
    [InlineData("Y06-10")]
    [InlineData("Y10-10")]
    public void Sem_o_grupo_de_cobranca_a_regra_nao_e_aplicavel(string regraId)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        NaoAplicavel(Aplicar(new DocumentoRegras(Data).Total("ICMSTot/vNF", 100.00m), regra), regra);
    }

    [Fact]
    public void YA03_20_pagamentos_acima_do_vNF_exigem_troco()
    {
        var regra = CatalogoRegrasTeste.Regra("YA03-20");

        Conforme(Aplicar(Pagamento(vNF: 100.00m, pagamentos: [60.00m, 50.00m], vTroco: 10.00m), regra), regra);
        Conforme(Aplicar(Pagamento(vNF: 100.00m, pagamentos: [60.00m, 40.00m]), regra), regra);
        Divergente(Aplicar(Pagamento(vNF: 100.00m, pagamentos: [60.00m, 50.00m]), regra), regra, "pag/vTroco");
    }

    [Fact]
    public void YA09_10_troco_e_a_soma_dos_pagamentos_menos_o_vNF_pendencia_C5()
    {
        var regra = CatalogoRegrasTeste.Regra("YA09-10");

        Conforme(Aplicar(Pagamento(vNF: 100.00m, pagamentos: [80.00m, 30.00m], vTroco: 10.00m), regra), regra);
        Divergente(
            Aplicar(Pagamento(vNF: 100.00m, pagamentos: [80.00m, 30.00m], vTroco: 20.00m), regra),
            regra, "pag/vTroco", informado: 20.00m, calculado: 10.00m);
    }

    [Fact]
    public void YA09_10_sem_troco_informado_nao_e_aplicavel()
    {
        var regra = CatalogoRegrasTeste.Regra("YA09-10");

        NaoAplicavel(Aplicar(Pagamento(vNF: 100.00m, pagamentos: [100.00m]), regra), regra);
    }

    [Theory]
    [InlineData("YA03-20")]
    [InlineData("YA09-10")]
    public void Sem_o_grupo_de_pagamento_a_regra_nao_e_aplicavel(string regraId)
    {
        var regra = CatalogoRegrasTeste.Regra(regraId);

        NaoAplicavel(Aplicar(new DocumentoRegras(Data).Total("ICMSTot/vNF", 100.00m), regra), regra);
    }

    private static DocumentoRegras Fatura(
        DateOnly data,
        decimal? vOrig = null,
        decimal? vDesc = null,
        decimal? vLiq = null,
        decimal[]? duplicatas = null)
    {
        var documento = new DocumentoRegras(data).Total("ICMSTot/vNF", vOrig ?? 100.00m).Campo("cobr/fat/nFat", "1");
        if (vOrig is not null)
        {
            documento.Campo("cobr/fat/vOrig", vOrig.Value);
        }

        if (vDesc is not null)
        {
            documento.Campo("cobr/fat/vDesc", vDesc.Value);
        }

        if (vLiq is not null)
        {
            documento.Campo("cobr/fat/vLiq", vLiq.Value);
        }

        for (var i = 0; i < (duplicatas?.Length ?? 0); i++)
        {
            documento.Campo($"cobr/dup[{i + 1}]/nDup", $"{i + 1:000}").Campo($"cobr/dup[{i + 1}]/vDup", duplicatas![i]);
        }

        return documento;
    }

    private static DocumentoRegras Pagamento(decimal vNF, decimal[] pagamentos, decimal? vTroco = null)
    {
        var documento = new DocumentoRegras(Data).Total("ICMSTot/vNF", vNF);
        for (var i = 0; i < pagamentos.Length; i++)
        {
            documento.Campo($"pag/detPag[{i + 1}]/tPag", "01").Campo($"pag/detPag[{i + 1}]/vPag", pagamentos[i]);
        }

        return vTroco is null ? documento : documento.Campo("pag/vTroco", vTroco.Value);
    }
}
