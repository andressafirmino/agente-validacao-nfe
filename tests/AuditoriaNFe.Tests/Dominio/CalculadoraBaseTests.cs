using AuditoriaNFe.Domain.Calculos;
using AuditoriaNFe.Domain.Documentos;

namespace AuditoriaNFe.Tests.Dominio;

/// <summary>Somas por campo catalogado, inclusões e exclusões (FR-031, T107).</summary>
public sealed class CalculadoraBaseTests
{
    private const string CampoVnf = "total/ICMSTot/vNF";
    private static readonly DateTimeOffset Agora = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Soma_campo_catalogado_em_documentos_validos()
    {
        var id = Guid.CreateVersion7();
        var documentos = new[]
        {
            Doc("35261111222333000181550010000009081071904650", "100.00"),
            Doc("35261111222333000181550010000009081071904651", "250.50"),
            Doc("35261111222333000181550010000009081071904652", "49.50"),
        };

        var registro = CalculadoraBase.SomarCampo(id, CampoVnf, documentos, chavesAcessoFiltro: null, incluirComErroEstrutural: false, Agora);

        Assert.Equal(id, registro.Id);
        Assert.Equal(OperacaoCalculo.Soma, registro.Operacao);
        Assert.Equal(400.00m, registro.Resultado);
        Assert.Equal(3, registro.DocumentosIncluidos.Count);
        Assert.Empty(registro.DocumentosExcluidos);
        Assert.All(registro.DocumentosIncluidos, d => Assert.False(d.ComRessalva));
    }

    [Fact]
    public void Documento_duplicado_excluido_com_motivo()
    {
        var chaveDuplicada = "35261111222333000181550010000009081071904650";
        var documentos = new[]
        {
            Doc(chaveDuplicada, "100.00"),
            Doc(chaveDuplicada, "100.00", MotivoExclusaoCalculo.Duplicado),
        };

        var registro = CalculadoraBase.SomarCampo(Guid.CreateVersion7(), CampoVnf, documentos, null, false, Agora);

        Assert.Equal(100.00m, registro.Resultado);
        Assert.Single(registro.DocumentosIncluidos);
        Assert.Single(registro.DocumentosExcluidos);
        Assert.Equal(MotivoExclusaoCalculo.Duplicado, registro.DocumentosExcluidos[0].Motivo);
        Assert.Equal(chaveDuplicada, registro.DocumentosExcluidos[0].ChaveAcesso);
    }

    [Fact]
    public void Erro_estrutural_excluido_sem_ressalva()
    {
        var chaveErro = "35261111222333000181550010000009081071904653";
        var documentos = new[]
        {
            Doc("35261111222333000181550010000009081071904650", "80.00"),
            Doc(chaveErro, "20.00", MotivoExclusaoCalculo.ErroEstrutural),
        };

        var registro = CalculadoraBase.SomarCampo(Guid.CreateVersion7(), CampoVnf, documentos, null, incluirComErroEstrutural: false, Agora);

        Assert.Equal(80.00m, registro.Resultado);
        Assert.Single(registro.DocumentosExcluidos);
        Assert.Equal(MotivoExclusaoCalculo.ErroEstrutural, registro.DocumentosExcluidos[0].Motivo);
    }

    [Fact]
    public void Erro_estrutural_incluido_com_ressalva_quando_solicitado()
    {
        var chaveErro = "35261111222333000181550010000009081071904653";
        var documentos = new[]
        {
            Doc("35261111222333000181550010000009081071904650", "80.00"),
            Doc(chaveErro, "20.00", MotivoExclusaoCalculo.ErroEstrutural),
        };

        var registro = CalculadoraBase.SomarCampo(Guid.CreateVersion7(), CampoVnf, documentos, null, incluirComErroEstrutural: true, Agora);

        Assert.Equal(100.00m, registro.Resultado);
        Assert.Equal(2, registro.DocumentosIncluidos.Count);
        Assert.Contains(registro.DocumentosIncluidos, d => d.ChaveAcesso == chaveErro && d.ComRessalva);
        Assert.Empty(registro.DocumentosExcluidos);
    }

    [Fact]
    public void Filtro_de_chaves_exclui_documentos_fora_da_lista()
    {
        var documentos = new[]
        {
            Doc("35261111222333000181550010000009081071904650", "10.00"),
            Doc("35261111222333000181550010000009081071904651", "90.00"),
        };
        var filtro = new HashSet<string>(StringComparer.Ordinal) { documentos[0].ChaveAcesso };

        var registro = CalculadoraBase.SomarCampo(Guid.CreateVersion7(), CampoVnf, documentos, filtro, false, Agora);

        Assert.Equal(10.00m, registro.Resultado);
        Assert.Single(registro.DocumentosIncluidos);
        Assert.Single(registro.DocumentosExcluidos);
        Assert.Equal(MotivoExclusaoCalculo.ForaDoFiltro, registro.DocumentosExcluidos[0].Motivo);
    }

    private static EntradaDocumentoCalculo Doc(string chave, string vNf, MotivoExclusaoCalculo? situacao = null) =>
        new(chave, [new CampoExtraido(CampoVnf, vNf)], situacao);
}
