using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Domain.Parecer;

/// <summary>
/// Parecer de uma análise (data-model §10), guardado na execução. O resultado geral, as limitações e
/// a revisão humana vêm de <see cref="CalculadoraResultadoGeral"/>, nunca da IA. A situação de cada
/// arquivo fica nos arquivos recebidos da execução.
/// </summary>
public sealed class ParecerExecucao
{
    private List<Achado> _achados = [];
    private List<string> _limitacoes = [];
    private List<ValidacaoNaoRealizada> _validacoesNaoRealizadas = [];

    private ParecerExecucao()
    {
        ResumoQuantitativo = new ResumoQuantitativo(0, 0, 0, 0);
        RevisaoHumana = new RevisaoHumana(false, []);
        ResumoTextual = string.Empty;
    }

    public static ParecerExecucao Criar(
        ResumoQuantitativo resumoQuantitativo,
        AvaliacaoParecer avaliacao,
        IReadOnlyList<Achado> achados,
        IReadOnlyList<ValidacaoNaoRealizada> validacoesNaoRealizadas,
        bool integracaoMcpHabilitada,
        string resumoTextual,
        string? versaoManifestoSchemas,
        string? versaoCatalogoRegras)
    {
        ArgumentNullException.ThrowIfNull(resumoQuantitativo);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(achados);
        ArgumentNullException.ThrowIfNull(validacoesNaoRealizadas);
        ArgumentException.ThrowIfNullOrWhiteSpace(resumoTextual);

        return new ParecerExecucao
        {
            ResumoQuantitativo = resumoQuantitativo,
            ResultadoGeral = avaliacao.Resultado,
            _achados = [.. achados],
            _limitacoes = [.. avaliacao.Limitacoes],
            _validacoesNaoRealizadas = [.. validacoesNaoRealizadas],
            RevisaoHumana = avaliacao.RevisaoHumana,
            IntegracaoMcpHabilitada = integracaoMcpHabilitada,
            ResumoTextual = resumoTextual,
            VersaoManifestoSchemas = versaoManifestoSchemas,
            VersaoCatalogoRegras = versaoCatalogoRegras,
        };
    }

    public ResumoQuantitativo ResumoQuantitativo { get; private set; }
    public ResultadoGeral ResultadoGeral { get; private set; }
    public IReadOnlyList<Achado> Achados => _achados;
    public IReadOnlyList<string> Limitacoes => _limitacoes;
    public IReadOnlyList<ValidacaoNaoRealizada> ValidacoesNaoRealizadas => _validacoesNaoRealizadas;
    public RevisaoHumana RevisaoHumana { get; private set; }
    public bool IntegracaoMcpHabilitada { get; private set; }

    /// <summary>Resumo da IA validada ou texto do sistema quando a IA não estiver disponível (FR-017).</summary>
    public string ResumoTextual { get; private set; }

    public string? VersaoManifestoSchemas { get; private set; }
    public string? VersaoCatalogoRegras { get; private set; }
}

/// <summary>Arquivos recebidos, analisados, com achados e não analisados (FR-017).</summary>
public sealed record ResumoQuantitativo(int Recebidos, int Analisados, int ComAchados, int NaoAnalisados);
