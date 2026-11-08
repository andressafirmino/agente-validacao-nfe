using System.Xml.Linq;
using AuditoriaNFe.Infrastructure.Schemas;
using AuditoriaNFe.Infrastructure.Xml;
using AuditoriaNFe.Tests.Fixtures.Xml;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>
/// TS-050 e SC-011: cada XML sintético de T044 é identificado, tem o pacote da sua pasta escolhido
/// pela data de referência e passa nesse pacote sem erro estrutural.
/// </summary>
public sealed class SchemasPorPeriodoTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 20);

    private static readonly Lazy<ValidadorXsd> Validador =
        new(() => new ValidadorXsd(new ProvedorPacotesSchema(ValidacaoXsdTeste.DiretorioSchemas)));

    public static TheoryData<string, string> Arquivos
    {
        get
        {
            var dados = new TheoryData<string, string>();
            foreach (var pacote in FixturesXml.Pacotes())
            {
                dados.Add(pacote.Id, pacote.ArquivoNFe);
                dados.Add(pacote.Id, pacote.ArquivoNFeProc);
            }

            return dados;
        }
    }

    [Theory]
    [MemberData(nameof(Arquivos))]
    public void Fixture_do_periodo_e_validada_no_pacote_da_sua_pasta_sem_erro_estrutural(string pacote, string arquivo)
    {
        var documento = Carregar(arquivo);

        var identificacao = IdentificadorNFe.Identificar(documento);
        Assert.True(identificacao.Suportado);

        var resultado = Validador.Value.Validar(documento, identificacao.DataReferencia!.Value, Hoje);

        Assert.Equal(pacote, resultado.PacoteVigente?.Id);
        Assert.Equal(pacote, resultado.PacoteAplicado?.Id);
        Assert.Equal([pacote], resultado.PacotesTentados);
        Assert.False(resultado.EmConvivencia);
        Assert.Empty(resultado.Erros);
    }

    [Fact]
    public void Todos_os_pacotes_do_manifesto_tem_fixture()
    {
        var provedor = new ProvedorPacotesSchema(ValidacaoXsdTeste.DiretorioSchemas);

        Assert.Equal(FixturesXml.Pacotes().Select(p => p.Id), provedor.Vigencias.Select(v => v.Id));
    }

    [Fact]
    public void Violacao_de_schema_informa_linha_posicao_e_pacote()
    {
        var erros = Validador.Value.ValidarNoPacote(Carregar(FixturesXml.Casos.ViolacaoSchema), FixturesXml.Casos.Pacote);

        var erro = Assert.Single(erros);
        Assert.Equal(FixturesXml.Casos.Pacote, erro.Pacote);
        Assert.True(erro.Linha > 0);
        Assert.True(erro.Posicao > 0);
        Assert.False(string.IsNullOrWhiteSpace(erro.Mensagem));
    }

    private static XDocument Carregar(string caminho)
    {
        var conteudo = FixturesXml.Todas().Single(f => f.Caminho == caminho).Conteudo;
        return XDocument.Load(new MemoryStream(conteudo), LoadOptions.SetLineInfo);
    }
}
