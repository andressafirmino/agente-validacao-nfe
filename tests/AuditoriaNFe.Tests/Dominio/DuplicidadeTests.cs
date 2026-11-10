using System.Security.Cryptography;
using System.Xml.Linq;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Validacao;
using AuditoriaNFe.Tests.Fixtures.Xml;

namespace AuditoriaNFe.Tests.Dominio;

/// <summary>Duplicidade por chave de acesso e SHA-256 (FR-009, ERR-011, ERR-012).</summary>
public sealed class DuplicidadeTests
{
    [Fact]
    public void Mesma_chave_e_mesmo_hash_e_duplicado_informativo_analisado_uma_vez_TS011()
    {
        var detector = new DetectorDuplicidade();
        var (chave1, hash1) = Dados(FixturesXml.Casos.Duplicado1);
        var (chave2, hash2) = Dados(FixturesXml.Casos.Duplicado2);
        Assert.Equal((chave1, hash1), (chave2, hash2));

        var primeiro = detector.Registrar("duplicado-1.xml", chave1, hash1);
        var segundo = detector.Registrar("duplicado-2.xml", chave2, hash2);

        Assert.Equal(new ResultadoDuplicidade(TipoDuplicidade.Nenhuma, null, null, true), primeiro);
        Assert.Equal(TipoDuplicidade.Duplicado, segundo.Tipo);
        Assert.Equal("duplicado-1.xml", segundo.ArquivoAnterior);
        Assert.Equal(Severidade.Informativa, segundo.Severidade);
        Assert.False(segundo.DeveAnalisar);
    }

    [Fact]
    public void Mesma_chave_e_hash_diferente_e_conteudo_divergente_alto_sem_descartar_TS012()
    {
        var detector = new DetectorDuplicidade();
        var (chave1, hash1) = Dados(FixturesXml.Casos.MesmaChave1);
        var (chave2, hash2) = Dados(FixturesXml.Casos.MesmaChave2);
        Assert.Equal(chave1, chave2);
        Assert.NotEqual(hash1, hash2);

        var primeiro = detector.Registrar("mesma-chave-1.xml", chave1, hash1);
        var segundo = detector.Registrar("mesma-chave-2.xml", chave2, hash2);

        Assert.True(primeiro.DeveAnalisar);
        Assert.Equal(TipoDuplicidade.ConteudoDivergente, segundo.Tipo);
        Assert.Equal("mesma-chave-1.xml", segundo.ArquivoAnterior);
        Assert.Equal(Severidade.Alta, segundo.Severidade);
        Assert.True(segundo.DeveAnalisar);
    }

    [Fact]
    public void Chaves_diferentes_nao_sao_duplicidade()
    {
        var detector = new DetectorDuplicidade();
        var (chaveA, hashA) = Dados(FixturesXml.Casos.Duplicado1);
        var (chaveB, hashB) = Dados(FixturesXml.Casos.MesmaChave1);

        detector.Registrar("a.xml", chaveA, hashA);
        var resultado = detector.Registrar("b.xml", chaveB, hashB);

        Assert.Equal(TipoDuplicidade.Nenhuma, resultado.Tipo);
        Assert.True(resultado.DeveAnalisar);
    }

    [Fact]
    public void Repeticao_e_detectada_contra_a_base_da_sessao()
    {
        var (chave, hash) = Dados(FixturesXml.Casos.MesmaChave1);
        var (_, hashDivergente) = Dados(FixturesXml.Casos.MesmaChave2);
        var sessao = new Dictionary<string, string> { [chave] = hash };

        var duplicado = new DetectorDuplicidade(sessao).Registrar("reenvio.xml", chave, hash);
        var divergente = new DetectorDuplicidade(sessao).Registrar("alterado.xml", chave, hashDivergente);

        Assert.Equal((TipoDuplicidade.Duplicado, (string?)null, false), (duplicado.Tipo, duplicado.ArquivoAnterior, duplicado.DeveAnalisar));
        Assert.Equal((TipoDuplicidade.ConteudoDivergente, true), (divergente.Tipo, divergente.DeveAnalisar));
    }

    private static (string Chave, string Hash) Dados(string caminho)
    {
        var conteudo = FixturesXml.Todas().Single(f => f.Caminho == caminho).Conteudo;
        XNamespace ns = "http://www.portalfiscal.inf.br/nfe";
        var id = (string)XDocument.Load(new MemoryStream(conteudo)).Descendants(ns + "infNFe").Single().Attribute("Id")!;
        return (id["NFe".Length..], Convert.ToHexStringLower(SHA256.HashData(conteudo)));
    }
}
