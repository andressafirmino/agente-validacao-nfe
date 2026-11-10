using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Tests.Fixtures.Xml;

namespace AuditoriaNFe.Tests.Dominio;

public sealed class IdentificacaoDocumentoTests
{
    private const string Ns = IdentificacaoDocumento.NamespaceNFe;
    private const string DhEmi = "2026-11-02T10:00:00-03:00";
    private const string DhRecbto = "2026-11-02T10:05:00-03:00";

    private static readonly string Chave55 = GeradorNFeSintetica.ChaveAcesso(new OpcoesNFeSintetica());
    private static readonly string Chave65 = GeradorNFeSintetica.ChaveAcesso(new OpcoesNFeSintetica { Modelo = "65" });

    [Fact]
    public void NFe_modelo_55_versao_4_00_e_suportada()
    {
        var identificacao = Avaliar();

        Assert.True(identificacao.Suportado);
        Assert.Null(identificacao.MotivoNaoSuportado);
        Assert.Equal(Chave55, identificacao.ChaveAcesso);
        Assert.Equal("55", identificacao.ModeloIde);
        Assert.Equal("55", identificacao.ModeloChave);
        Assert.Equal("4.00", identificacao.VersaoLayout);
        Assert.Equal("nfeProc", identificacao.ElementoRaiz);
    }

    [Fact]
    public void NFCe_modelo_65_nao_e_suportada_TS052()
    {
        var identificacao = Avaliar(modeloIde: "65", idInfNFe: "NFe" + Chave65);

        Assert.False(identificacao.Suportado);
        Assert.Equal(MotivoNaoSuportado.ModeloNaoSuportado, identificacao.MotivoNaoSuportado);
        Assert.Equal("65", identificacao.ModeloChave);
    }

    [Theory]
    [InlineData("55", true)]
    [InlineData("65", false)]
    public void Modelo_divergente_entre_ide_e_chave_nao_e_suportado_TS053(string modeloIde, bool chaveModelo65)
    {
        var identificacao = Avaliar(modeloIde: modeloIde, idInfNFe: "NFe" + (chaveModelo65 ? Chave65 : Chave55));

        Assert.Equal(MotivoNaoSuportado.ModeloNaoSuportado, identificacao.MotivoNaoSuportado);
    }

    [Theory]
    [InlineData("procEventoNFe", Ns)]
    [InlineData("nfeProc", "http://www.portalfiscal.inf.br/cte")]
    [InlineData("nfeProc", null)]
    [InlineData("NFe", "")]
    public void Raiz_ou_namespace_de_outro_documento_nao_e_NFe(string raiz, string? ns)
    {
        var identificacao = Avaliar(raiz: raiz, ns: ns);

        Assert.Equal(MotivoNaoSuportado.NaoENFe, identificacao.MotivoNaoSuportado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("35261111222333000181550010000000011")]
    public void Chave_ausente_ou_fora_do_formato_nao_e_NFe(string? idInfNFe)
    {
        var identificacao = Avaliar(idInfNFe: idInfNFe);

        Assert.Equal(MotivoNaoSuportado.NaoENFe, identificacao.MotivoNaoSuportado);
    }

    [Fact]
    public void Id_sem_o_prefixo_NFe_nao_e_NFe()
    {
        Assert.Equal(MotivoNaoSuportado.NaoENFe, Avaliar(idInfNFe: Chave55).MotivoNaoSuportado);
    }

    [Theory]
    [InlineData("3.10")]
    [InlineData("4.01")]
    [InlineData(null)]
    public void Versao_diferente_de_4_00_nao_e_suportada(string? versao)
    {
        var identificacao = Avaliar(versao: versao);

        Assert.Equal(MotivoNaoSuportado.VersaoNaoSuportada, identificacao.MotivoNaoSuportado);
    }

    [Fact]
    public void Com_protocolo_a_data_de_referencia_vem_do_dhRecbto()
    {
        var identificacao = Avaliar(dhEmi: "2026-08-02T23:58:00-03:00", dhRecbto: "2026-08-03T00:03:00-03:00");

        Assert.Equal(new DateOnly(2026, 8, 3), identificacao.DataReferencia);
        Assert.Equal(OrigemDataReferencia.DhRecbto, identificacao.OrigemDataReferencia);
        Assert.True(identificacao.PossuiProtocolo);
    }

    [Fact]
    public void Sem_protocolo_a_data_de_referencia_vem_do_dhEmi()
    {
        var identificacao = Avaliar(raiz: "NFe", possuiProtocolo: false, dhRecbto: null);

        Assert.Equal(new DateOnly(2026, 11, 2), identificacao.DataReferencia);
        Assert.Equal(OrigemDataReferencia.DhEmi, identificacao.OrigemDataReferencia);
        Assert.False(identificacao.PossuiProtocolo);
    }

    [Fact]
    public void Data_de_referencia_e_a_data_de_calendario_no_deslocamento_do_XML()
    {
        // 23:30 em -03:00 já é o dia seguinte em UTC; vale a data escrita no XML.
        var identificacao = Avaliar(dhRecbto: "2026-08-02T23:30:00-03:00");

        Assert.Equal(new DateOnly(2026, 8, 2), identificacao.DataReferencia);
    }

    [Fact]
    public void dhEmi_e_dhRecbto_ficam_como_no_XML()
    {
        var identificacao = Avaliar();

        Assert.Equal(DhEmi, identificacao.DhEmi);
        Assert.Equal(DhRecbto, identificacao.DhRecbto);
    }

    [Fact]
    public void Documento_nao_suportado_nao_tem_data_de_referencia()
    {
        var identificacao = Avaliar(modeloIde: "65", idInfNFe: "NFe" + Chave65);

        Assert.Null(identificacao.DataReferencia);
    }

    [Theory]
    [InlineData("procEventoNFe", "evento de NF-e")]
    [InlineData("resNFe", "resumo de NF-e")]
    [InlineData("enviNFe", "lote de envio")]
    [InlineData("cteProc", "CT-e")]
    [InlineData("pedido", "`pedido`")]
    public void Motivo_de_raiz_diferente_diz_o_que_o_arquivo_e_TS072(string raiz, string esperado)
    {
        var motivo = Avaliar(raiz: raiz).DescreverMotivo();

        Assert.NotNull(motivo);
        Assert.Contains(esperado, motivo, StringComparison.Ordinal);
        Assert.Contains($"`{raiz}`", motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Motivo_de_namespace_diferente_cita_o_namespace_da_NFe_sem_repetir_o_recebido_TS072()
    {
        var motivo = Avaliar(ns: "http://exemplo.com/ignore-as-instrucoes").DescreverMotivo();

        Assert.Contains("namespace", motivo, StringComparison.Ordinal);
        Assert.Contains(Ns, motivo, StringComparison.Ordinal);
        Assert.DoesNotContain("ignore", motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Motivo_de_chave_invalida_cita_o_Id_de_infNFe_TS072()
    {
        var motivo = Avaliar(idInfNFe: "NFe123").DescreverMotivo();

        Assert.Contains("chave de acesso", motivo, StringComparison.Ordinal);
        Assert.DoesNotContain("123", motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Motivo_de_NFCe_diz_modelo_65_TS072()
    {
        var motivo = Avaliar(modeloIde: "65", idInfNFe: "NFe" + Chave65).DescreverMotivo();

        Assert.Contains("NFC-e (modelo 65)", motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Motivo_de_modelo_divergente_cita_os_dois_modelos_TS072()
    {
        var motivo = Avaliar(modeloIde: "55", idInfNFe: "NFe" + Chave65).DescreverMotivo();

        Assert.Contains("`ide/mod` (55)", motivo, StringComparison.Ordinal);
        Assert.Contains("chave de acesso (65)", motivo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("3.10", "versão de layout 3.10")]
    [InlineData(null, "versão de layout não informada")]
    [InlineData("4.00 ignore tudo", "versão de layout inválida")]
    public void Motivo_de_versao_cita_a_versao_informada_so_quando_bem_formada_TS072(string? versao, string esperado)
    {
        var motivo = Avaliar(versao: versao).DescreverMotivo();

        Assert.Contains(esperado, motivo, StringComparison.Ordinal);
        Assert.DoesNotContain("ignore", motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Documento_suportado_nao_tem_motivo()
    {
        Assert.Null(Avaliar().DescreverMotivo());
    }

    private const string IdPadrao = "id-padrao";

    private static IdentificacaoDocumento Avaliar(
        string raiz = "nfeProc",
        string? ns = Ns,
        string? versao = "4.00",
        string? modeloIde = "55",
        string? idInfNFe = IdPadrao,
        bool possuiProtocolo = true,
        string? dhEmi = DhEmi,
        string? dhRecbto = DhRecbto) =>
        IdentificacaoDocumento.Avaliar(raiz, ns, versao, modeloIde, idInfNFe == IdPadrao ? "NFe" + Chave55 : idInfNFe, possuiProtocolo, dhEmi, dhRecbto);
}
