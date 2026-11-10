using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Domain.Regras;

/// <summary>
/// Aplica a um documento a versão de cada regra vigente na data de referência (FR-013a). Regra sem
/// versão vigente não é avaliada nem registrada. Cada divergência vira achado
/// <see cref="CategoriaAchado.InconsistenciaNegocio"/> com a severidade do catálogo (FR-014, FR-023).
/// </summary>
public sealed class AplicadorRegras
{
    private readonly IReadOnlyList<RegraConteudo> _regras;
    private readonly Dictionary<string, IImplementacaoRegra> _implementacoes;
    private readonly string? _versaoCatalogo;

    /// <param name="versaoCatalogo">Versão do catálogo de regras; com ela, cada achado leva também a evidência da regra.</param>
    public AplicadorRegras(IReadOnlyList<RegraConteudo> regras, IEnumerable<IImplementacaoRegra> implementacoes, string? versaoCatalogo = null)
    {
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(implementacoes);
        _regras = regras;
        _versaoCatalogo = versaoCatalogo;
        _implementacoes = implementacoes.ToDictionary(i => i.Id, StringComparer.Ordinal);

        var semImplementacao = regras.Where(r => !_implementacoes.ContainsKey(r.Implementacao)).Select(r => r.Implementacao).ToList();
        if (semImplementacao.Count > 0)
        {
            throw new ArgumentException($"Regras sem implementação: {string.Join(", ", semImplementacao)}.", nameof(implementacoes));
        }
    }

    public ResultadoAplicacaoRegras Aplicar(DadosDocumento documento, Func<Guid> gerarId)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(gerarId);

        var achados = new List<Achado>();
        var aplicadas = new List<RegraAplicada>();
        foreach (var regra in _regras.Where(r => r.VigenteEm(documento.DataReferencia)))
        {
            var avaliacao = _implementacoes[regra.Implementacao].Avaliar(documento, regra);
            aplicadas.Add(new RegraAplicada(regra.Id, regra.VersaoRegra, avaliacao.Resultado, avaliacao.Motivo));
            if (avaliacao.Resultado != ResultadoRegra.Divergente)
            {
                continue;
            }

            achados.AddRange(avaliacao.Divergencias.Select(d => new Achado(
                gerarId(),
                CategoriaAchado.InconsistenciaNegocio,
                regra.Severidade,
                OrigemAchado.Deterministica,
                DescricaoAchadoRegra.Montar(regra, d),
                arquivo: documento.Arquivo,
                chaveAcesso: documento.ChaveAcesso,
                campo: d.Campo,
                item: d.Item,
                valorInformado: d.ValorInformado,
                valorEsperado: d.ValorEsperado,
                regraId: regra.Id,
                versaoRegra: regra.VersaoRegra,
                evidencias: Evidencias(documento, regra, d))));
        }

        return new ResultadoAplicacaoRegras(achados, aplicadas);
    }

    private List<Evidencia> Evidencias(DadosDocumento documento, RegraConteudo regra, DivergenciaRegra divergencia)
    {
        var evidencias = new List<Evidencia> { Evidencia.CampoXml(documento.ChaveAcesso, divergencia.Campo, divergencia.ValorInformado) };
        if (!string.IsNullOrWhiteSpace(_versaoCatalogo))
        {
            evidencias.Add(Evidencia.RegraCatalogo(regra.Id, regra.VersaoRegra, _versaoCatalogo));
        }

        return evidencias;
    }
}

public sealed record ResultadoAplicacaoRegras(IReadOnlyList<Achado> Achados, IReadOnlyList<RegraAplicada> RegrasAplicadas);
