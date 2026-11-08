using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using AuditoriaNFe.Tests.Arquitetura;
using Casos = AuditoriaNFe.Tests.Fixtures.Xml.FixturesXml.Casos;

namespace AuditoriaNFe.Tests.Fixtures.Xml;

/// <summary>
/// Os XMLs versionados em <c>Fixtures/Xml</c> são os produzidos por <see cref="FixturesXml"/>. Para
/// regravá-los depois de mudar o gerador, execute os testes com <c>ATUALIZAR_FIXTURES_XML=1</c>.
/// </summary>
public sealed class FixturesXmlTests
{
    private const string VariavelAtualizacao = "ATUALIZAR_FIXTURES_XML";
    private static readonly XNamespace Nfe = GeradorNFeSintetica.Nfe;
    private static readonly string Diretorio = Path.Combine(Repositorio.Raiz(), "tests", "AuditoriaNFe.Tests", "Fixtures", "Xml");

    [Fact]
    public void Arquivos_versionados_sao_iguais_aos_gerados()
    {
        var fixtures = FixturesXml.Todas();
        if (Environment.GetEnvironmentVariable(VariavelAtualizacao) == "1")
        {
            foreach (var fixture in fixtures)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Caminho(fixture.Caminho))!);
                File.WriteAllBytes(Caminho(fixture.Caminho), fixture.Conteudo);
            }
        }

        var divergentes = fixtures
            .Where(f => !File.Exists(Caminho(f.Caminho)) || !File.ReadAllBytes(Caminho(f.Caminho)).AsSpan().SequenceEqual(f.Conteudo))
            .Select(f => f.Caminho)
            .ToList();
        Assert.True(divergentes.Count == 0,
            $"Fixtures ausentes ou diferentes do gerador (regrave com {VariavelAtualizacao}=1): {string.Join(", ", divergentes)}");

        var emDisco = Directory.EnumerateFiles(Diretorio, "*.xml", SearchOption.AllDirectories)
            .Select(c => Path.GetRelativePath(Diretorio, c).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);
        Assert.Equal(fixtures.Select(f => f.Caminho).Order(StringComparer.Ordinal), emDisco);
    }

    [Fact]
    public void Ha_uma_pasta_por_pacote_do_manifesto_alem_dos_casos()
    {
        var pastas = Directory.EnumerateDirectories(Diretorio).Select(Path.GetFileName).Order(StringComparer.Ordinal);

        Assert.Equal(FixturesXml.Pacotes().Select(p => p.Id).Append("Casos").Order(StringComparer.Ordinal), pastas);
    }

    public static TheoryData<string> Pacotes() => [.. FixturesXml.Pacotes().Select(p => p.Id)];

    [Theory]
    [MemberData(nameof(Pacotes))]
    public void Fixtures_do_pacote_sao_validas_no_schema_e_datadas_na_vigencia(string id)
    {
        var pacote = FixturesXml.Pacotes().Single(p => p.Id == id);
        var nfe = Carregar(pacote.ArquivoNFe);
        var nfeProc = Carregar(pacote.ArquivoNFeProc);

        Assert.Equal("NFe", nfe.Root!.Name.LocalName);
        Assert.Equal("nfeProc", nfeProc.Root!.Name.LocalName);
        Assert.Empty(ValidacaoXsdTeste.Validar(nfe, id, "nfe_v4.00.xsd"));
        Assert.Empty(ValidacaoXsdTeste.Validar(nfeProc, id, "procNFe_v4.00.xsd"));
        Assert.True(pacote.Contem(DataCalendario(nfe, "dhEmi")));
        Assert.True(pacote.Contem(DataCalendario(nfeProc, "dhRecbto")));
        Assert.NotEqual(Chave(nfe), Chave(nfeProc));
    }

    [Fact]
    public void Casos_validos_no_schema_e_casos_com_erro_estrutural()
    {
        string[] validos = [Casos.NFCeModelo65, Casos.ModeloDivergente, Casos.InstrucoesInfCpl, Casos.TotalizadorDivergente, Casos.Duplicado1, Casos.MesmaChave1, Casos.MesmaChave2];
        foreach (var caso in validos)
        {
            Assert.True(ValidarNfeProc(caso).Count == 0, $"{caso} deveria ser válido no schema do {Casos.Pacote}.");
        }

        Assert.NotEmpty(ValidarNfeProc(Casos.ViolacaoSchema));
        Assert.NotEmpty(ValidarNfeProc(Casos.LayoutESomatorio));
        Assert.Throws<XmlException>(() => Carregar(Casos.Malformado));
    }

    [Fact]
    public void Casos_de_identificacao_trazem_o_dado_nao_suportado()
    {
        var nfce = Carregar(Casos.NFCeModelo65);
        Assert.Equal("65", Elemento(nfce, "mod").Value);
        Assert.Equal("65", Chave(nfce)[20..22]);

        var divergente = Carregar(Casos.ModeloDivergente);
        Assert.Equal("55", Elemento(divergente, "mod").Value);
        Assert.Equal("65", Chave(divergente)[20..22]);

        var versao310 = Carregar(Casos.Versao310);
        Assert.Equal("3.10", Elemento(versao310, "infNFe").Attribute("versao")!.Value);

        var evento = Carregar(Casos.EventoCancelamento);
        Assert.Equal(Nfe + "procEventoNFe", evento.Root!.Name);
        Assert.Equal("110111", Elemento(evento, "tpEvento").Value);
    }

    [Fact]
    public void Casos_de_conteudo_e_duplicidade_trazem_a_divergencia()
    {
        Assert.Equal(Casos.TextoInstrucoes, Elemento(Carregar(Casos.InstrucoesInfCpl), "infCpl").Value);

        foreach (var caso in new[] { Casos.TotalizadorDivergente, Casos.LayoutESomatorio })
        {
            var documento = Carregar(caso);
            var somaItens = documento.Descendants(Nfe + "ICMS00").Sum(i => decimal.Parse(i.Element(Nfe + "vICMS")!.Value, CultureInfo.InvariantCulture));
            var total = decimal.Parse(Elemento(documento, "ICMSTot").Element(Nfe + "vICMS")!.Value, CultureInfo.InvariantCulture);
            Assert.Equal(somaItens + 1m, total);
        }

        Assert.Equal(Ler(Casos.Duplicado1), Ler(Casos.Duplicado2));

        var mesmaChave1 = Carregar(Casos.MesmaChave1);
        var mesmaChave2 = Carregar(Casos.MesmaChave2);
        Assert.Equal(Chave(mesmaChave1), Chave(mesmaChave2));
        Assert.NotEqual(Ler(Casos.MesmaChave1), Ler(Casos.MesmaChave2));
    }

    private static string Caminho(string relativo) => Path.Combine(Diretorio, relativo);

    private static byte[] Ler(string relativo) => File.ReadAllBytes(Caminho(relativo));

    private static XDocument Carregar(string relativo)
    {
        using var conteudo = new MemoryStream(Ler(relativo));
        return XDocument.Load(conteudo);
    }

    private static List<string> ValidarNfeProc(string relativo) =>
        ValidacaoXsdTeste.Validar(Carregar(relativo), Casos.Pacote, "procNFe_v4.00.xsd");

    private static XElement Elemento(XDocument documento, string nome) => documento.Descendants(Nfe + nome).First();

    private static string Chave(XDocument documento) => Elemento(documento, "infNFe").Attribute("Id")!.Value["NFe".Length..];

    /// <summary>Data de calendário como escrita no XML, no deslocamento do próprio documento.</summary>
    private static DateOnly DataCalendario(XDocument documento, string campo) =>
        DateOnly.FromDateTime(DateTimeOffset.Parse(Elemento(documento, campo).Value, CultureInfo.InvariantCulture).DateTime);
}
