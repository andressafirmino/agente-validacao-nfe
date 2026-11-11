using System.Text.Json;
using Api.Agente.A2A.Sofia;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Tests.Arquitetura;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>Schemas e validação do contrato SOFIA v1.0 (T140, FR-043a, TS-066).</summary>
public sealed class ContratoSofiaTests
{
    private static string PastaFixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Sofia");
    private static string PastaContratos => Path.Combine(Repositorio.Raiz(), "specs", "001-agente-auditoria-nfe", "contracts");

    [Fact]
    public void Fixtures_Sofia_iguais_aos_exemplos_do_contrato()
    {
        var origem = Path.Combine(Repositorio.Raiz(), "specs", "001-agente-auditoria-nfe", "contracts", "exemplos");
        foreach (var arquivo in Directory.EnumerateFiles(origem, "*.json"))
        {
            var nome = Path.GetFileName(arquivo);
            var copia = Path.Combine(PastaFixtures, nome);
            Assert.True(File.Exists(copia), $"Fixture ausente: {nome}");
            Assert.Equal(File.ReadAllText(arquivo), File.ReadAllText(copia));
        }
    }

    [Theory]
    [InlineData("solicitacao-analise-v1.json", "sofia-solicitacao-analise-v1.schema.json")]
    [InlineData("resultado-analise-v1-parcial.json", "sofia-resultado-analise-v1.schema.json")]
    [InlineData("resultado-analise-v1-erro-contrato.json", "sofia-resultado-analise-v1.schema.json")]
    public void Exemplos_validos_atendem_o_schema(string exemplo, string schema)
    {
        var conteudo = File.ReadAllText(Path.Combine(PastaFixtures, exemplo));
        using var elemento = JsonDocument.Parse(conteudo);
        var esquema = schema.Contains("solicitacao", StringComparison.Ordinal)
            ? EsquemasSofiaTeste.SolicitacaoAnalise
            : EsquemasSofiaTeste.ResultadoAnalise;
        var resultado = esquema.Evaluate(elemento.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Flag });
        Assert.True(resultado.IsValid, string.Join("; ", resultado.Errors?.Select(e => e.Value) ?? []));
    }

    [Fact]
    public void Exemplo_invalido_falha_no_schema_de_solicitacao()
    {
        var conteudo = File.ReadAllText(Path.Combine(PastaFixtures, "solicitacao-versao-invalida.json"));
        using var elemento = JsonDocument.Parse(conteudo);
        var esquema = EsquemasSofiaTeste.SolicitacaoAnalise;
        var resultado = esquema.Evaluate(elemento.RootElement);
        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Serializacao_DTOs_em_camelCase_ida_e_volta()
    {
        var original = new SolicitacaoAnaliseSofia(
            ContratoSofiaV1.Versao,
            Guid.Parse("8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e"),
            Guid.Parse("e1b3a4cb-d729-4bd4-b796-b9c94be07b46"),
            new UsuarioSofia("Maria"),
            "Pergunta",
            [new DocumentoSofia("doc-1", "a.xml", "<nfe/>")]);
        var json = JsonSerializer.Serialize(original, ContratoSofiaV1.Json);
        Assert.Contains("\"versaoContrato\"", json, StringComparison.Ordinal);
        Assert.Contains("\"idDocumento\"", json, StringComparison.Ordinal);
        var volta = JsonSerializer.Deserialize<SolicitacaoAnaliseSofia>(json, ContratoSofiaV1.Json);
        Assert.NotNull(volta);
        Assert.Equal(original.Pergunta, volta.Pergunta);
    }

    [Fact]
    public void Campos_desconhecidos_sao_ignorados_na_desserializacao()
    {
        const string json =
            """
            {"versaoContrato":"1.0","idExecucao":"8b2f0ad6-c2e4-4a68-8bdd-20f785bc798e","idSolicitacao":"e1b3a4cb-d729-4bd4-b796-b9c94be07b46","usuario":{"nome":"Maria"},"pergunta":"x","documentos":[{"idDocumento":"d1","nome":"a.xml","texto":"<n/>"}],"extra":true}
            """;
        Assert.True(ValidadorSolicitacaoSofia.TentarDesserializar(json, out var solicitação, out _));
        Assert.NotNull(solicitação);
    }

    [Theory]
    [InlineData("2.0", CodigoErro.MensagemForaDoContrato)]
    [InlineData("", CodigoErro.SolicitacaoSemConteudo)]
    public void Validador_aplica_regras_adicionais(string variante, string codigoEsperado)
    {
        var validador = CriarValidador();
        SolicitacaoAnaliseSofia solicitacao = variante switch
        {
            "2.0" => BaseSolicitacao() with { VersaoContrato = "2.0" },
            _ => BaseSolicitacao() with { Documentos = [] },
        };
        var validacao = validador.Validar(solicitacao);
        Assert.False(validacao.Valida);
        Assert.Equal(codigoEsperado, validacao.CodigoErro);
    }

    [Fact]
    public void Validador_recusa_nome_longo_e_pergunta_longa()
    {
        var validador = CriarValidador();
        Assert.Equal(CodigoErro.MensagemForaDoContrato, validador.Validar(BaseSolicitacao() with { Usuario = new UsuarioSofia(new string('n', 201)) }).CodigoErro);
        Assert.Equal(CodigoErro.MensagemForaDoContrato, validador.Validar(BaseSolicitacao() with { Pergunta = new string('p', 4_001) }).CodigoErro);
    }

  [Fact]
    public void Validador_recusa_idDocumento_repetido_e_tamanho_total()
    {
        var validador = CriarValidador(limiteBytes: 50);
        var repetido = BaseSolicitacao() with
        {
            Documentos =
            [
                new DocumentoSofia("d", "a.xml", "<a/>"),
                new DocumentoSofia("d", "b.xml", "<b/>"),
            ],
        };
        Assert.Equal(CodigoErro.MensagemForaDoContrato, validador.Validar(repetido).CodigoErro);
        Assert.Equal(CodigoErro.TamanhoExcedido, validador.Validar(BaseSolicitacao() with
        {
            Documentos = [new DocumentoSofia("d1", "a.xml", new string('x', 60))],
        }).CodigoErro);
    }

    private static ValidadorSolicitacaoSofia CriarValidador(int limiteBytes = 5_242_880, int maxArquivos = 20) =>
        new(Options.Create(new LimitesOpcoes { TamanhoTotalBytes = limiteBytes, QuantidadeArquivos = maxArquivos }));

    private static SolicitacaoAnaliseSofia BaseSolicitacao() =>
        new(
            ContratoSofiaV1.Versao,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new UsuarioSofia("Maria"),
            "Pergunta",
            [new DocumentoSofia("doc-1", "a.xml", "<nfe/>")]);
}
