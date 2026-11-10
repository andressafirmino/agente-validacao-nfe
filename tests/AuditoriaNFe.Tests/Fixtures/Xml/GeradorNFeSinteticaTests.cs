using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using AuditoriaNFe.Domain.Documentos;

namespace AuditoriaNFe.Tests.Fixtures.Xml;

public sealed class GeradorNFeSinteticaTests
{
    public static TheoryData<string, string> Pacotes()
    {
        using var manifesto = JsonDocument.Parse(File.ReadAllText(Path.Combine(ValidacaoXsdTeste.DiretorioSchemas, "manifesto.json")));
        var dados = new TheoryData<string, string>();
        foreach (var pacote in manifesto.RootElement.GetProperty("pacotes").EnumerateArray())
        {
            dados.Add(pacote.GetProperty("id").GetString()!, pacote.GetProperty("vigenciaInicio").GetString()!);
        }

        return dados;
    }

    [Theory]
    [MemberData(nameof(Pacotes))]
    public void Documento_minimo_e_valido_no_schema_de_cada_pacote(string pacote, string vigenciaInicio)
    {
        var dhEmi = new DateTimeOffset(DateOnly.Parse(vigenciaInicio, CultureInfo.InvariantCulture), new TimeOnly(10, 0), TimeSpan.FromHours(-3));

        var opcoes = GeradorNFeSintetica.OpcoesParaPacote(pacote, dhEmi);

        var comProtocolo = GeradorNFeSintetica.Gerar(opcoes);
        var semProtocolo = GeradorNFeSintetica.Gerar(opcoes with { ComProtocolo = false });

        Assert.Empty(Validar(comProtocolo, pacote, "procNFe_v4.00.xsd"));
        Assert.Empty(Validar(semProtocolo, pacote, "nfe_v4.00.xsd"));
    }

    [Fact]
    public void Chave_de_acesso_tem_44_digitos_dv_correto_e_corresponde_ao_ide()
    {
        var opcoes = new OpcoesNFeSintetica { Numero = 1234, Serie = 2 };

        var documento = GeradorNFeSintetica.Gerar(opcoes);
        var chave = GeradorNFeSintetica.ChaveAcesso(opcoes);

        Assert.True(ChaveAcessoNFe.FormatoValido(chave));
        Assert.Equal(GeradorNFeSintetica.DigitoChave(chave[..43]), chave[43] - '0');
        Assert.Equal("NFe" + chave, Elemento(documento, "infNFe").Attribute("Id")!.Value);
        Assert.Equal(chave, Elemento(documento, "chNFe").Value);
        Assert.Equal(chave[^1..], Elemento(documento, "cDV").Value);
        Assert.NotEqual(Elemento(documento, "cNF").Value, opcoes.Numero.ToString("D8", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Digito_da_chave_segue_o_modulo_11()
    {
        // Restos 0 e 1 geram DV 0; nos demais casos, DV = 11 - resto.
        Assert.Equal(0, GeradorNFeSintetica.DigitoChave(new string('0', 43)));
        Assert.Equal(9, GeradorNFeSintetica.DigitoChave("1"));
    }

    [Fact]
    public void Cnpj_ficticio_tem_digitos_verificadores()
    {
        Assert.Equal("11222333000181", GeradorNFeSintetica.Cnpj("112223330001"));
    }

    [Fact]
    public void Totais_sao_a_soma_coerente_dos_itens()
    {
        var documento = GeradorNFeSintetica.Gerar(new OpcoesNFeSintetica
        {
            Itens = [new("ITEM A", 3m, 10.10m), new("ITEM B", 1m, 0.99m)],
        });

        Assert.Equal("31.29", Elemento(documento, "ICMSTot").Element(GeradorNFeSintetica.Nfe + "vProd")!.Value);
        Assert.Equal("5.63", Elemento(documento, "ICMSTot").Element(GeradorNFeSintetica.Nfe + "vICMS")!.Value);
        Assert.Equal("31.29", Elemento(documento, "vNF").Value);
        Assert.Equal("31.29", Elemento(documento, "vPag").Value);
    }

    [Fact]
    public void Recebimento_tem_deslocamento_e_atraso_configurado()
    {
        var documento = GeradorNFeSintetica.Gerar(new OpcoesNFeSintetica { AtrasoRecebimento = TimeSpan.FromHours(2) });

        Assert.Equal("2026-11-02T10:00:00-03:00", Elemento(documento, "dhEmi").Value);
        Assert.Equal("2026-11-02T12:00:00-03:00", Elemento(documento, "dhRecbto").Value);
    }

    [Fact]
    public void Ajuste_permite_montar_casos_de_erro()
    {
        var documento = GeradorNFeSintetica.Gerar(new OpcoesNFeSintetica
        {
            ComProtocolo = false,
            Ajustar = xml => Elemento(xml, "vNF").Value = "999.99",
        });

        Assert.Equal("999.99", Elemento(documento, "vNF").Value);
        Assert.Empty(Validar(documento, "PL_010g_v1.03", "nfe_v4.00.xsd"));
    }

    private static XElement Elemento(XDocument documento, string nome) =>
        documento.Descendants(GeradorNFeSintetica.Nfe + nome).First();

    private static List<string> Validar(XDocument documento, string pacote, string schema) =>
        ValidacaoXsdTeste.Validar(documento, pacote, schema);
}
