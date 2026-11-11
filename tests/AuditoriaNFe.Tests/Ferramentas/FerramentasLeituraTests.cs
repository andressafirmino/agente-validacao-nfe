using System.Text.Json;
using AuditoriaNFe.Application.Ferramentas;
using AuditoriaNFe.Domain.Calculos;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Domain.Regras;
using AuditoriaNFe.Domain.Validacao;
using AuditoriaNFe.Infrastructure.Catalogos;
using AuditoriaNFe.Tests.Arquitetura;
using AuditoriaNFe.Tests.Duplos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using AuditoriaNFe.Domain.Identificacao;

namespace AuditoriaNFe.Tests.Ferramentas;

/// <summary>Contrato ferramentas-agente.md v1, política, timeout e schemas (T109).</summary>
public sealed class FerramentasLeituraTests
{
    private const string CampoVnf = "total/ICMSTot/vNF";
    private const string Chave = "35261111222333000181550010000009081071904650";

    [Fact]
    public void Cada_ferramenta_tem_schema_coerente_com_o_contrato()
    {
        var ferramentas = CriarFerramentas();
        var esperados = new Dictionary<string, int>
        {
            [PoliticaFerramentas.ListarDocumentos] = 0,
            [PoliticaFerramentas.ObterCamposExtraidos] = 2,
            [PoliticaFerramentas.CalcularTotal] = 3,
            [PoliticaFerramentas.ListarAchados] = 3,
        };

        foreach (var funcao in ferramentas.CriarFuncoes())
        {
            Assert.True(esperados.ContainsKey(funcao.Name));
            Assert.False(string.IsNullOrWhiteSpace(funcao.Name));
        }
    }

    [Theory]
    [InlineData(TipoExecucao.AnaliseLote, PoliticaFerramentas.CalcularTotal, false)]
    [InlineData(TipoExecucao.Pergunta, PoliticaFerramentas.CalcularTotal, true)]
    [InlineData(TipoExecucao.Pergunta, PoliticaFerramentas.ListarDocumentos, true)]
    [InlineData(TipoExecucao.AnaliseLote, PoliticaFerramentas.ListarAchados, true)]
    public void Politica_por_tipo_de_execucao(TipoExecucao tipo, string nome, bool permitida) =>
        Assert.Equal(permitida, PoliticaFerramentas.Autorizada(tipo, nome));

    [Fact]
    public void Campo_nao_catalogado_retorna_erro()
    {
        using var escopo = Escopo(Doc(CampoVnf, "10"));
        var resultado = CriarFerramentas().ObterCamposExtraidos(Chave, ["campo.inventado"]);
        AssertErro(resultado, CodigoErroFerramenta.CampoNaoCatalogado);
    }

    [Fact]
    public void Argumento_invalido_rejeita_chave_curta()
    {
        using var escopo = Escopo();
        var resultado = CriarFerramentas().ObterCamposExtraidos("123", [CampoVnf]);
        AssertErro(resultado, CodigoErroFerramenta.ArgumentoInvalido);
    }

    [Fact]
    public void Calcular_total_registra_calculo_na_execucao()
    {
        var execucao = ExecucaoFixture();
        using var escopo = EscopoComExec(execucao, [Doc(CampoVnf, "123.45")]);
        var saida = CriarFerramentas().CalcularTotal(CampoVnf);
        var calculoId = AssertCalculo(saida, execucao);
        Assert.Single(execucao.Calculos);
        Assert.Equal(calculoId, execucao.Calculos[0].Id);
    }

    private static FerramentasLeitura CriarFerramentas()
    {
        var catalogo = Catalogo();
        return new FerramentasLeitura(catalogo, new RelogioControlado(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero)), new IGeradorIdsStub());
    }

    private static CatalogoRegras Catalogo()
    {
        var raiz = Repositorio.Raiz();
        var caminhoRegras = Path.Combine(raiz, "src", "AuditoriaNFe.Infrastructure", "Catalogos", "catalogo-regras.v1.json");
        var caminhoExtracao = Path.Combine(raiz, "src", "AuditoriaNFe.Infrastructure", "Catalogos", "catalogo-extracao.v1.json");
        return new CatalogoRegras(CarregadorCatalogos.Carregar(caminhoRegras, caminhoExtracao, ImplementacoesRegras.Todas()));
    }

    private static IDisposable Escopo(params DocumentoFerramenta[] docs)
    {
        var execucao = ExecucaoFixture();
        return EscopoComExec(execucao, docs);
    }

    private static IDisposable EscopoComExec(Execucao execucao, IReadOnlyList<DocumentoFerramenta> docs)
    {
        var estado = MontadorEstadoFerramentas.Montar(docs, [], []);
        return EscopoFerramentasExecucao.Iniciar(execucao, estado);
    }

    private static Execucao ExecucaoFixture() =>
        IdentificacaoSolicitante.TentarCriar("anonimo", "teste", out var id) && id is { } identificacao
            ? Execucao.Receber(
            Guid.CreateVersion7(),
            identificacao,
            CanalExecucao.Direto,
            TipoExecucao.Pergunta,
            new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero))
            : throw new InvalidOperationException("Identificação inválida.");

    private static DocumentoFerramenta Doc(string campo, string valor) =>
        new(
            Chave,
            "nfe.xml",
            new DateOnly(2026, 11, 2),
            nameof(OrigemDataReferencia.DhEmi),
            "pl_008i2",
            nameof(SituacaoValidacao.Realizada),
            null,
            nameof(SituacaoValidacao.Realizada),
            null,
            null,
            null,
            [new CampoExtraido(campo, valor)]);

    private static void AssertErro(object resultado, string codigo)
    {
        var json = JsonSerializer.Serialize(resultado);
        using var doc = JsonDocument.Parse(json);
        var erro = doc.RootElement.GetProperty("erro");
        var lido = erro.TryGetProperty("codigo", out var cod) ? cod.GetString() : erro.GetProperty("Codigo").GetString();
        Assert.Equal(codigo, lido);
    }

    private static Guid AssertCalculo(object saida, Execucao execucao)
    {
        var json = JsonSerializer.Serialize(saida);
        using var doc = JsonDocument.Parse(json);
        var idTexto = doc.RootElement.GetProperty("calculoId").GetString();
        Assert.True(Guid.TryParse(idTexto, out var id));
        Assert.Equal(id, execucao.Calculos[0].Id);
        return id;
    }

    private sealed class IGeradorIdsStub : AuditoriaNFe.Application.Abstracoes.IGeradorIds
    {
        public Guid Novo() => Guid.Parse("01934567-89ab-7def-0123-456789abcdef");
    }
}
