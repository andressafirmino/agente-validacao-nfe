using System.Globalization;
using AuditoriaNFe.Domain.Regras;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Tests.Dominio.Regras;

/// <summary>Aplica regras do catálogo de teste com as implementações do Domínio e confere o resultado.</summary>
internal static class AplicacaoRegrasTeste
{
    public static ResultadoAplicacaoRegras Aplicar(DocumentoRegras documento, params RegraConteudo[] regras) =>
        new AplicadorRegras(regras, ImplementacoesRegras.Todas()).Aplicar(documento.Construir(), Guid.CreateVersion7);

    public static ResultadoAplicacaoRegras Aplicar(DocumentoRegras documento, IReadOnlyList<RegraConteudo> regras) =>
        new AplicadorRegras(regras, ImplementacoesRegras.Todas()).Aplicar(documento.Construir(), Guid.CreateVersion7);

    public static void Conforme(ResultadoAplicacaoRegras resultado, RegraConteudo regra)
    {
        Assert.Empty(resultado.Achados);
        var aplicada = Assert.Single(resultado.RegrasAplicadas);
        Assert.Equal((regra.Id, regra.VersaoRegra, ResultadoRegra.Conforme), (aplicada.RegraId, aplicada.VersaoRegra, aplicada.Resultado));
    }

    public static void NaoAplicavel(ResultadoAplicacaoRegras resultado, RegraConteudo regra)
    {
        Assert.Empty(resultado.Achados);
        var aplicada = Assert.Single(resultado.RegrasAplicadas);
        Assert.Equal((regra.Id, regra.VersaoRegra, ResultadoRegra.NaoAplicavel), (aplicada.RegraId, aplicada.VersaoRegra, aplicada.Resultado));
        Assert.False(string.IsNullOrWhiteSpace(aplicada.Motivo));
    }

    public static void NaoAvaliada(ResultadoAplicacaoRegras resultado)
    {
        Assert.Empty(resultado.Achados);
        Assert.Empty(resultado.RegrasAplicadas);
    }

    /// <summary>Uma divergência no campo, com os valores informado e calculado (FR-014).</summary>
    public static Achado Divergente(
        ResultadoAplicacaoRegras resultado,
        RegraConteudo regra,
        string campo,
        decimal? informado = null,
        decimal? calculado = null)
    {
        var aplicada = Assert.Single(resultado.RegrasAplicadas);
        Assert.Equal((regra.Id, regra.VersaoRegra, ResultadoRegra.Divergente), (aplicada.RegraId, aplicada.VersaoRegra, aplicada.Resultado));

        var achado = Assert.Single(resultado.Achados);
        Assert.Equal(CategoriaAchado.InconsistenciaNegocio, achado.Categoria);
        Assert.Equal(OrigemAchado.Deterministica, achado.Origem);
        Assert.Equal(regra.Severidade, achado.Severidade);
        Assert.Equal(regra.Id, achado.RegraId);
        Assert.Equal(regra.VersaoRegra, achado.VersaoRegra);
        Assert.Equal(DocumentoRegras.Arquivo, achado.Arquivo);
        Assert.Equal(DocumentoRegras.Chave, achado.ChaveAcesso);
        Assert.Equal(campo, achado.Campo);
        Assert.False(string.IsNullOrWhiteSpace(achado.Descricao));

        if (informado is not null)
        {
            Assert.Equal(informado.Value, decimal.Parse(achado.ValorInformado!, CultureInfo.InvariantCulture));
        }

        if (calculado is not null)
        {
            Assert.Equal(calculado.Value, decimal.Parse(achado.ValorEsperado!, CultureInfo.InvariantCulture));
        }

        return achado;
    }
}
