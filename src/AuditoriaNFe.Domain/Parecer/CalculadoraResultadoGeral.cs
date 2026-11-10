using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Domain.Parecer;

/// <summary>
/// Calcula o resultado geral, as limitações e a revisão humana pelas regras de data-model §10
/// (FR-019, FR-024, FR-047b, FR-052c). O contexto do usuário não participa do cálculo (FR-058).
/// </summary>
public static class CalculadoraResultadoGeral
{
    public static AvaliacaoParecer Calcular(EntradaResultadoGeral entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var deterministicos = entrada.Achados.Any(a =>
            a.Categoria is CategoriaAchado.ErroEstrutural or CategoriaAchado.InconsistenciaNegocio);
        var incompleta =
            entrada.Etapas.Values.Any(e => e is EstadoEtapa.Falhou or EstadoEtapa.NaoExecutada)
            || entrada.ValidacoesNaoRealizadas.Count > 0
            || entrada.DependenciaIndisponivel
            || entrada.Achados.Any(a => a.Categoria == CategoriaAchado.FalhaTecnica);

        var resultado = deterministicos
            ? ResultadoGeral.ComInconsistencias
            : incompleta ? ResultadoGeral.Inconclusiva : ResultadoGeral.SemInconsistencias;

        var limitacoes = new List<string>();
        var motivos = new List<string>();

        if (resultado == ResultadoGeral.Inconclusiva)
        {
            motivos.Add(RevisaoHumana.MotivoResultadoInconclusivo);
        }

        if (entrada.Achados.Any(a => a.Categoria == CategoriaAchado.IndicioIa && a.Severidade is Severidade.Alta or Severidade.Media))
        {
            motivos.Add(RevisaoHumana.MotivoIndicioIa);
        }

        if (entrada.Achados.Any(a => a.Categoria == CategoriaAchado.AusenciaEvidencias))
        {
            motivos.Add(RevisaoHumana.MotivoAusenciaEvidencias);
        }

        if (!entrada.IntegracaoMcpHabilitada)
        {
            limitacoes.Add(LimitacoesParecer.ConsultaManuaisNaoHabilitada);
            motivos.Add(RevisaoHumana.MotivoConsultaManuaisNaoHabilitada);
        }

        if (!entrada.UsuarioIdentificado)
        {
            limitacoes.Add(LimitacoesParecer.UsuarioNaoIdentificado);
        }

        if (entrada.SemBaseAnalisada)
        {
            limitacoes.Add(LimitacoesParecer.SemBaseAnalisada);
        }

        return new AvaliacaoParecer(resultado, limitacoes, new RevisaoHumana(motivos.Count > 0, motivos));
    }
}

/// <summary>Situação da execução que o cálculo do resultado geral considera.</summary>
public sealed record EntradaResultadoGeral
{
    /// <summary>Estado de cada uma das nove etapas, pelo número.</summary>
    public required IReadOnlyDictionary<int, EstadoEtapa> Etapas { get; init; }

    public IReadOnlyList<ValidacaoNaoRealizada> ValidacoesNaoRealizadas { get; init; } = [];

    /// <summary>Base de conhecimento, provedor de IA ou outra dependência indisponível (ERR-013 a ERR-015).</summary>
    public bool DependenciaIndisponivel { get; init; }

    public IReadOnlyList<Achado> Achados { get; init; } = [];
    public required bool IntegracaoMcpHabilitada { get; init; }
    public required bool UsuarioIdentificado { get; init; }

    /// <summary>Pergunta numa sessão sem nenhum documento analisado (FR-042).</summary>
    public bool SemBaseAnalisada { get; init; }
}

/// <summary><c>Validacao</c> é <c>Layout</c> ou <c>Conteudo</c>.</summary>
public sealed record ValidacaoNaoRealizada(string Arquivo, string Validacao, string Motivo);

public sealed record AvaliacaoParecer(ResultadoGeral Resultado, IReadOnlyList<string> Limitacoes, RevisaoHumana RevisaoHumana);
