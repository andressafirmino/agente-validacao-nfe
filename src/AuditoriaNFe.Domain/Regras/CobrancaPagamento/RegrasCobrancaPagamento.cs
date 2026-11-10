namespace AuditoriaNFe.Domain.Regras.CobrancaPagamento;

/// <summary>Regras dos grupos Y (cobrança) e YA (pagamento) do catálogo aprovado (Anexo I, pp. 125–126).</summary>
internal static class RegrasCobrancaPagamento
{
    public static IReadOnlyList<IImplementacaoRegra> Todas { get; } =
    [
        new RegraY05(),
        new RegraY06(),
        new RegraY10(),
        new RegraYA03_20(),
        new RegraYA09(),
    ];
}

internal static class CamposCobranca
{
    public const string Fatura = "cobr/fat/";
    public const string NFat = "cobr/fat/nFat";
    public const string VOrig = "cobr/fat/vOrig";
    public const string VDesc = "cobr/fat/vDesc";
    public const string VLiq = "cobr/fat/vLiq";
    public const string VDup = "cobr/dup[*]/vDup";
    public const string NDup = "cobr/dup[*]/nDup";
    public const string Pagamento = "pag/";
    public const string VPag = "pag/detPag[*]/vPag";
    public const string TPag = "pag/detPag[*]/tPag";
    public const string VTroco = "pag/vTroco";
    public const string VNF = "total/ICMSTot/vNF";

    public static AvaliacaoRegra SemFatura() => AvaliacaoRegra.NaoAplicavel("Grupo de cobrança (cobr/fat) ausente no documento.");

    public static AvaliacaoRegra SemPagamento() => AvaliacaoRegra.NaoAplicavel("Grupo de pagamento (pag) ausente no documento.");
}

/// <summary>Y05-10: desconto da fatura não pode ser maior que o valor original; ausentes valem zero.</summary>
internal sealed class RegraY05() : ImplementacaoRegraBase("Y05-10.v1", [CamposCobranca.NFat, CamposCobranca.VOrig, CamposCobranca.VDesc])
{
    protected override AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra)
    {
        if (!documento.TemGrupo(CamposCobranca.Fatura))
        {
            return CamposCobranca.SemFatura();
        }

        var vDesc = documento.ValorOuZero(CamposCobranca.VDesc);
        var vOrig = documento.ValorOuZero(CamposCobranca.VOrig);
        return vDesc > vOrig
            ? AvaliacaoRegra.Divergente(new DivergenciaRegra(CamposCobranca.VDesc, Texto(vDesc), Texto(vOrig)))
            : AvaliacaoRegra.Conforme();
    }
}

/// <summary>Y06-10: vLiq = vOrig − vDesc, só quando vLiq e vOrig forem informados.</summary>
internal sealed class RegraY06() : ImplementacaoRegraBase("Y06-10.v1", [CamposCobranca.NFat, CamposCobranca.VOrig, CamposCobranca.VDesc, CamposCobranca.VLiq])
{
    protected override AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra)
    {
        if (!documento.TemGrupo(CamposCobranca.Fatura))
        {
            return CamposCobranca.SemFatura();
        }

        if (documento.Valor(CamposCobranca.VLiq) is not { } vLiq || documento.Valor(CamposCobranca.VOrig) is not { } vOrig)
        {
            return AvaliacaoRegra.NaoAplicavel("vLiq ou vOrig da fatura não informado.");
        }

        return Comparar(CamposCobranca.VLiq, vLiq, vOrig - documento.ValorOuZero(CamposCobranca.VDesc), regra);
    }
}

/// <summary>Y10-10: Σ das duplicatas vDup = vLiq da fatura.</summary>
internal sealed class RegraY10() : ImplementacaoRegraBase("Y10-10.v1", [CamposCobranca.NFat, CamposCobranca.VLiq, CamposCobranca.VDup, CamposCobranca.NDup])
{
    protected override AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra)
    {
        if (!documento.TemGrupo("cobr/"))
        {
            return AvaliacaoRegra.NaoAplicavel("Grupo de cobrança (cobr) ausente no documento.");
        }

        if (documento.Valor(CamposCobranca.VLiq) is not { } vLiq || !documento.Existe(CamposCobranca.VDup))
        {
            return AvaliacaoRegra.NaoAplicavel("vLiq da fatura ou duplicatas não informados.");
        }

        return Comparar(CamposCobranca.VLiq, vLiq, documento.Soma(CamposCobranca.VDup), regra);
    }
}

/// <summary>YA03-20: se Σ vPag for maior que vNF, o troco deve ser informado.</summary>
internal sealed class RegraYA03_20() : ImplementacaoRegraBase("YA03-20.v1", [CamposCobranca.VPag, CamposCobranca.TPag, CamposCobranca.VTroco, CamposCobranca.VNF])
{
    protected override AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra)
    {
        if (!documento.TemGrupo(CamposCobranca.Pagamento))
        {
            return CamposCobranca.SemPagamento();
        }

        var excedente = documento.Soma(CamposCobranca.VPag) - documento.ValorOuZero(CamposCobranca.VNF);
        return excedente > 0 && !documento.Tem(CamposCobranca.VTroco)
            ? AvaliacaoRegra.Divergente(new DivergenciaRegra(CamposCobranca.VTroco, "ausente", Texto(excedente)))
            : AvaliacaoRegra.Conforme();
    }
}

/// <summary>YA09-10: troco informado = Σ vPag − vNF (soma dos pagamentos, pendência C5).</summary>
internal sealed class RegraYA09() : ImplementacaoRegraBase("YA09-10.v1", [CamposCobranca.VPag, CamposCobranca.TPag, CamposCobranca.VTroco, CamposCobranca.VNF])
{
    protected override AvaliacaoRegra Avaliar(LeituraDocumento documento, RegraConteudo regra)
    {
        if (!documento.TemGrupo(CamposCobranca.Pagamento))
        {
            return CamposCobranca.SemPagamento();
        }

        if (documento.Valor(CamposCobranca.VTroco) is not { } vTroco)
        {
            return AvaliacaoRegra.NaoAplicavel("Troco não informado.");
        }

        return Comparar(CamposCobranca.VTroco, vTroco, documento.Soma(CamposCobranca.VPag) - documento.ValorOuZero(CamposCobranca.VNF), regra);
    }
}
