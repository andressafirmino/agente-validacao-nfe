using System.Text.Json.Nodes;
using AuditoriaNFe.Domain.Regras;
using AuditoriaNFe.Infrastructure.Catalogos;
using AuditoriaNFe.Infrastructure.Comum;
using AuditoriaNFe.Infrastructure.Schemas;
using AuditoriaNFe.Tests.Dominio.Regras;
using AuditoriaNFe.Tests.Fixtures.Xml;

namespace AuditoriaNFe.Tests.Integracao;

/// <summary>Validações de carga do manifesto de schemas e dos catálogos (data-model §5 e §6).</summary>
public sealed class CargaSchemasCatalogosTests : IDisposable
{
    private readonly string _temporario = Path.Combine(Path.GetTempPath(), "auditoria-carga-" + Guid.NewGuid().ToString("N"));

    public CargaSchemasCatalogosTests() => Directory.CreateDirectory(_temporario);

    public void Dispose() => Directory.Delete(_temporario, recursive: true);

    [Fact]
    public void Manifesto_versionado_carrega_os_19_pacotes()
    {
        var provedor = new ProvedorPacotesSchema(ValidacaoXsdTeste.DiretorioSchemas);

        Assert.Equal(
            FixturesXml.Pacotes().Select(p => (p.Id, p.VigenciaInicio, p.VigenciaFim)),
            provedor.Vigencias.Select(v => (v.Id, v.Inicio, v.Fim)));
        Assert.Equal(19, provedor.Vigencias.Count);
        Assert.NotNull(provedor.Esquemas("PL_010g_v1.03"));
    }

    [Fact]
    public void Falha_com_SHA256_divergente()
    {
        var diretorio = CopiarSchemas();
        File.AppendAllText(Path.Combine(diretorio, "PL_010e_v1.02", "tiposBasico_v4.00.xsd"), " ");

        Assert.Throws<CargaInvalidaException>(() => new ProvedorPacotesSchema(diretorio));
    }

    [Fact]
    public void Falha_com_XSD_ausente()
    {
        var diretorio = CopiarSchemas();
        File.Delete(Path.Combine(diretorio, "PL_009k_NT2023_001_v120", "leiauteNFe_v4.00.xsd"));

        Assert.Throws<CargaInvalidaException>(() => new ProvedorPacotesSchema(diretorio));
    }

    [Fact]
    public void Falha_com_arquivo_fora_do_manifesto()
    {
        var diretorio = CopiarSchemas();
        File.WriteAllText(Path.Combine(diretorio, "PL_010g_v1.03", "extra.xsd"), "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"/>");

        Assert.Throws<CargaInvalidaException>(() => new ProvedorPacotesSchema(diretorio));
    }

    [Fact]
    public void Falha_com_vigencias_sobrepostas()
    {
        var diretorio = CopiarSchemas();
        AlterarManifesto(diretorio, pacotes => pacotes[1]!["vigenciaInicio"] = pacotes[0]!["vigenciaFim"]!.GetValue<string>());

        Assert.Throws<CargaInvalidaException>(() => new ProvedorPacotesSchema(diretorio));
    }

    [Fact]
    public void Falha_com_lacuna_entre_vigencias()
    {
        var diretorio = CopiarSchemas();
        AlterarManifesto(diretorio, pacotes =>
        {
            var fim = DateOnly.Parse(pacotes[0]!["vigenciaFim"]!.GetValue<string>());
            pacotes[1]!["vigenciaInicio"] = fim.AddDays(2).ToString("yyyy-MM-dd");
        });

        Assert.Throws<CargaInvalidaException>(() => new ProvedorPacotesSchema(diretorio));
    }

    [Fact]
    public void Falha_com_fim_aberto_fora_do_ultimo_pacote()
    {
        var diretorio = CopiarSchemas();
        AlterarManifesto(diretorio, pacotes => pacotes[0]!["vigenciaFim"] = null);

        Assert.Throws<CargaInvalidaException>(() => new ProvedorPacotesSchema(diretorio));
    }

    [Fact]
    public void Catalogos_versionados_carregam_e_espelham_o_catalogo_aprovado()
    {
        var catalogos = CarregadorCatalogos.Carregar(
            Path.Combine(AppContext.BaseDirectory, "Catalogos", "catalogo-regras.v1.json"),
            Path.Combine(AppContext.BaseDirectory, "Catalogos", "catalogo-extracao.v1.json"),
            ImplementacoesRegras.Todas());

        Assert.Equal(
            CatalogoRegrasTeste.Todas.Select(Resumo).Order(),
            catalogos.Regras.Select(Resumo).Order());
        Assert.All(catalogos.Regras, r => Assert.False(string.IsNullOrWhiteSpace(r.Fonte.Documento)));
    }

    [Fact]
    public void Catalogo_minimo_valido_carrega()
    {
        var (regras, extracao) = Catalogos();

        var catalogos = CarregadorCatalogos.Carregar(regras, extracao, [Implementacao]);

        var regra = Assert.Single(catalogos.Regras);
        Assert.Equal(("W04-10", 1, "W04-10.v1"), (regra.Id, regra.VersaoRegra, regra.Implementacao));
        Assert.Equal("1.0-teste", catalogos.VersaoCatalogoRegras);
    }

    [Fact]
    public void Falha_com_regra_sem_fonte()
    {
        var (regras, extracao) = Catalogos(r => r.Remove("fonte"));

        Assert.Throws<CargaInvalidaException>(() => CarregadorCatalogos.Carregar(regras, extracao, [Implementacao]));
    }

    [Fact]
    public void Falha_com_regra_sem_vigencia()
    {
        var (regras, extracao) = Catalogos(r => r.Remove("vigencia"));

        Assert.Throws<CargaInvalidaException>(() => CarregadorCatalogos.Carregar(regras, extracao, [Implementacao]));
    }

    [Fact]
    public void Falha_com_implementacao_inexistente()
    {
        var (regras, extracao) = Catalogos(r => r["implementacao"] = "W04-10.v9");

        Assert.Throws<CargaInvalidaException>(() => CarregadorCatalogos.Carregar(regras, extracao, [Implementacao]));
    }

    [Fact]
    public void Falha_com_campo_fora_do_catalogo_de_extracao()
    {
        var (regras, extracao) = Catalogos();
        var implementacao = new ImplementacaoFalsa("W04-10.v1", ["total/ICMSTot/vICMS", "det[*]/imposto/ICMS/*/vInexistente"]);

        Assert.Throws<CargaInvalidaException>(() => CarregadorCatalogos.Carregar(regras, extracao, [implementacao]));
    }

    [Fact]
    public void Falha_com_versoes_sobrepostas()
    {
        var (regras, extracao) = Catalogos(adicionais:
        [
            Regra(versao: 2, inicio: "2020-01-01", fim: null, implementacao: "W04-10.v2"),
        ]);
        var implementacoes = new IImplementacaoRegra[] { Implementacao, new ImplementacaoFalsa("W04-10.v2", Implementacao.Campos) };

        Assert.Throws<CargaInvalidaException>(() => CarregadorCatalogos.Carregar(regras, extracao, implementacoes));
    }

    [Fact]
    public void Versoes_consecutivas_sem_sobreposicao_carregam()
    {
        var (regras, extracao) = Catalogos(
            r => r["vigencia"]!["fim"] = "2019-12-31",
            [Regra(versao: 2, inicio: "2020-01-01", fim: null, implementacao: "W04-10.v2")]);
        var implementacoes = new IImplementacaoRegra[] { Implementacao, new ImplementacaoFalsa("W04-10.v2", Implementacao.Campos) };

        Assert.Equal(2, CarregadorCatalogos.Carregar(regras, extracao, implementacoes).Regras.Count);
    }

    private static readonly ImplementacaoFalsa Implementacao =
        new("W04-10.v1", ["total/ICMSTot/vICMS", "det[*]/imposto/ICMS/*/vICMS", "det[*]/imposto/ICMS/*/CST"]);

    private static string Resumo(RegraConteudo r) =>
        $"{r.Id}|{r.VersaoRegra}|{r.Grupo}|{r.VigenciaInicio:yyyy-MM-dd}|{r.VigenciaFim:yyyy-MM-dd}|{r.Severidade}|{r.Tolerancia}|" +
        $"{r.AplicacaoSefaz.Tipo}|{r.AplicacaoSefaz.CodigoRejeicao}|{r.Implementacao}";

    private (string Regras, string Extracao) Catalogos(Action<JsonObject>? alterarRegra = null, JsonObject[]? adicionais = null)
    {
        var regra = Regra(versao: 1, inicio: "2017-12-04", fim: null, implementacao: "W04-10.v1");
        alterarRegra?.Invoke(regra);
        var lista = new JsonArray(regra);
        foreach (var adicional in adicionais ?? [])
        {
            lista.Add(adicional);
        }

        var regras = new JsonObject
        {
            ["versaoCatalogo"] = "1.0-teste",
            ["aprovacao"] = new JsonObject { ["por"] = "Solicitante da feature", ["em"] = "2026-11-04" },
            ["fontes"] = new JsonArray(new JsonObject
            {
                ["documento"] = "MOC 7.0 Anexo I",
                ["versao"] = "7.03",
                ["sha256"] = new string('a', 64),
            }),
            ["regras"] = lista,
        };
        var extracao = new JsonObject
        {
            ["versaoCatalogo"] = "1.0-teste",
            ["campos"] = new JsonArray(
                Campo("total/ICMSTot/vICMS"),
                Campo("det[*]/imposto/ICMS/*/vICMS"),
                Campo("det[*]/imposto/ICMS/*/CST", "string")),
        };

        var caminhoRegras = Path.Combine(_temporario, "catalogo-regras.json");
        var caminhoExtracao = Path.Combine(_temporario, "catalogo-extracao.json");
        File.WriteAllText(caminhoRegras, regras.ToJsonString());
        File.WriteAllText(caminhoExtracao, extracao.ToJsonString());
        return (caminhoRegras, caminhoExtracao);
    }

    private static JsonObject Campo(string caminho, string tipo = "decimal") =>
        new() { ["caminho"] = caminho, ["tipo"] = tipo, ["classe"] = "Fiscal" };

    private static JsonObject Regra(int versao, string inicio, string? fim, string implementacao) => new()
    {
        ["id"] = "W04-10",
        ["versaoRegra"] = versao,
        ["grupo"] = "ICMSTot",
        ["descricao"] = "vICMS = soma dos vICMS dos itens, sem CST 40, 41 e 50",
        ["fonte"] = new JsonObject { ["documento"] = "MOC 7.0 Anexo I", ["versao"] = "7.03", ["paginas"] = "120" },
        ["vigencia"] = new JsonObject { ["inicio"] = inicio, ["fim"] = fim },
        ["severidade"] = "Alta",
        ["tolerancia"] = null,
        ["aplicacaoSefaz"] = new JsonObject { ["tipo"] = "Facultativa", ["codigoRejeicao"] = "532" },
        ["condicoes"] = new JsonArray("Não somar itens com CST 40, 41 e 50"),
        ["implementacao"] = implementacao,
    };

    private string CopiarSchemas()
    {
        var destino = Path.Combine(_temporario, "NFe");
        foreach (var arquivo in Directory.EnumerateFiles(ValidacaoXsdTeste.DiretorioSchemas, "*", SearchOption.AllDirectories))
        {
            var alvo = Path.Combine(destino, Path.GetRelativePath(ValidacaoXsdTeste.DiretorioSchemas, arquivo));
            Directory.CreateDirectory(Path.GetDirectoryName(alvo)!);
            File.Copy(arquivo, alvo);
        }

        return destino;
    }

    private static void AlterarManifesto(string diretorio, Action<JsonArray> alterar)
    {
        var caminho = Path.Combine(diretorio, "manifesto.json");
        var manifesto = JsonNode.Parse(File.ReadAllText(caminho))!;
        alterar(manifesto["pacotes"]!.AsArray());
        File.WriteAllText(caminho, manifesto.ToJsonString());
    }

    private sealed class ImplementacaoFalsa(string id, IReadOnlyList<string> campos) : IImplementacaoRegra
    {
        public string Id => id;
        public IReadOnlyList<string> Campos => campos;

        public AvaliacaoRegra Avaliar(DadosDocumento documento, RegraConteudo regra) => AvaliacaoRegra.Conforme();
    }
}
