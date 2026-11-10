using System.Globalization;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Parecer;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Application.Servicos;

/// <summary>
/// Monta o parecer da execução (FR-017, FR-010d, data-model §10): resumo quantitativo, achados,
/// limitações, validações não realizadas, revisão humana, integração MCP, versões e resumo textual
/// da IA validada ou, sem ela, de modelo de texto do sistema. O resultado geral vem de
/// <see cref="CalculadoraResultadoGeral"/>. A visão do contrato é derivada só do que foi gravado,
/// para que a consulta da execução devolva o mesmo parecer da criação.
/// </summary>
public static class MontadorParecer
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static ParecerExecucao Montar(
        Execucao execucao,
        IReadOnlyList<Achado> achados,
        IReadOnlyList<ValidacaoNaoRealizada> validacoesNaoRealizadas,
        bool dependenciaIndisponivel,
        string? resumoIa,
        string? versaoManifestoSchemas,
        string? versaoCatalogoRegras,
        bool semBaseAnalisada = false)
    {
        ArgumentNullException.ThrowIfNull(execucao);
        ArgumentNullException.ThrowIfNull(achados);
        ArgumentNullException.ThrowIfNull(validacoesNaoRealizadas);

        var avaliacao = CalculadoraResultadoGeral.Calcular(new EntradaResultadoGeral
        {
            Etapas = execucao.Etapas.ToDictionary(e => e.Numero, e => e.Estado),
            ValidacoesNaoRealizadas = validacoesNaoRealizadas,
            DependenciaIndisponivel = dependenciaIndisponivel,
            Achados = achados,
            IntegracaoMcpHabilitada = execucao.IntegracaoMcpHabilitada,
            UsuarioIdentificado = execucao.UsuarioIdentificado,
            SemBaseAnalisada = semBaseAnalisada,
        });
        var resumo = ResumoQuantitativo(execucao.Arquivos, achados);

        return ParecerExecucao.Criar(
            resumo,
            avaliacao,
            achados,
            validacoesNaoRealizadas,
            execucao.IntegracaoMcpHabilitada,
            string.IsNullOrWhiteSpace(resumoIa) ? ResumoDoSistema(resumo, avaliacao.Resultado, execucao.Arquivos, achados) : resumoIa,
            versaoManifestoSchemas,
            versaoCatalogoRegras);
    }

    /// <summary>Analisado é o arquivo com layout validado; com achados, o analisado com achado determinístico ou indício.</summary>
    public static ResumoQuantitativo ResumoQuantitativo(IReadOnlyList<ArquivoRecebido> arquivos, IReadOnlyList<Achado> achados)
    {
        ArgumentNullException.ThrowIfNull(arquivos);
        ArgumentNullException.ThrowIfNull(achados);

        var analisados = arquivos.Where(a => a.ValidacaoLayout.Situacao == SituacaoValidacao.Realizada).ToList();
        var comAchados = analisados.Count(a => achados.Any(x =>
            x.Arquivo == a.Nome && x.Categoria is CategoriaAchado.ErroEstrutural or CategoriaAchado.InconsistenciaNegocio or CategoriaAchado.IndicioIa));
        return new ResumoQuantitativo(arquivos.Count, analisados.Count, comAchados, arquivos.Count - analisados.Count);
    }

    public static ParecerV1 ParaContrato(Execucao execucao)
    {
        ArgumentNullException.ThrowIfNull(execucao);
        var parecer = execucao.Parecer ?? throw new InvalidOperationException("A execução não tem parecer.");

        return new ParecerV1(
            new ResumoQuantitativoV1(
                parecer.ResumoQuantitativo.Recebidos,
                parecer.ResumoQuantitativo.Analisados,
                parecer.ResumoQuantitativo.ComAchados,
                parecer.ResumoQuantitativo.NaoAnalisados),
            parecer.ResultadoGeral.ToString(),
            [.. execucao.Arquivos.Select(Documento)],
            [.. parecer.Achados.Select(Achado)],
            [.. parecer.Limitacoes],
            [.. parecer.ValidacoesNaoRealizadas.Select(v => new ValidacaoNaoRealizadaV1(v.Arquivo, v.Validacao, v.Motivo))],
            new RevisaoHumanaV1(parecer.RevisaoHumana.Necessaria, [.. parecer.RevisaoHumana.Motivos]),
            [],
            new IntegracaoMcpV1(parecer.IntegracaoMcpHabilitada),
            parecer.ResumoTextual,
            parecer.VersaoManifestoSchemas is null && parecer.VersaoCatalogoRegras is null
                ? null
                : new VersoesParecerV1(parecer.VersaoManifestoSchemas, parecer.VersaoCatalogoRegras));
    }

    private static DocumentoParecerV1 Documento(ArquivoRecebido arquivo) =>
        new(
            arquivo.Nome,
            arquivo.ChaveAcesso,
            arquivo.ValidacaoLayout.DataReferencia,
            arquivo.ValidacaoLayout.OrigemDataReferencia?.ToString(),
            arquivo.ValidacaoLayout.PacoteAplicado,
            [.. arquivo.ValidacaoLayout.PacotesTentados],
            arquivo.ValidacaoLayout.EmConvivencia,
            arquivo.ValidacaoLayout.Estado.ToString(),
            arquivo.ValidacaoConteudo.ToString(),
            arquivo.DuplicadoDe);

    private static AchadoV1 Achado(Achado achado) =>
        new(
            achado.Id,
            achado.Categoria.ToString(),
            achado.Severidade.ToString(),
            achado.Origem.ToString(),
            achado.Descricao,
            achado.Arquivo,
            achado.ChaveAcesso,
            achado.Campo,
            achado.Linha,
            achado.Posicao,
            achado.Item,
            achado.ValorInformado,
            achado.ValorEsperado,
            achado.RegraId,
            achado.VersaoRegra,
            achado.PacoteSchema,
            [.. achado.Evidencias.Select(Evidencia)]);

    private static EvidenciaV1 Evidencia(Evidencia evidencia) =>
        new(
            evidencia.Tipo.ToString(),
            evidencia.ChaveAcesso,
            evidencia.Caminho,
            evidencia.Valor,
            evidencia.RegraId,
            evidencia.VersaoRegra,
            evidencia.VersaoCatalogo,
            evidencia.ChunkId,
            evidencia.Documento,
            evidencia.Versao,
            evidencia.Trecho,
            evidencia.CalculoId);

    /// <summary>
    /// Resumo montado pelo sistema quando a análise por IA não está disponível (FR-017b): contagens,
    /// resultado, até <see cref="MaximoInconsistenciasNoResumo"/> inconsistências, arquivos não
    /// analisados com o motivo e alertas. Fala em "inconsistências" e "alertas", nunca em "achados".
    /// </summary>
    public static string ResumoDoSistema(
        ResumoQuantitativo resumo,
        ResultadoGeral resultado,
        IReadOnlyList<ArquivoRecebido> arquivos,
        IReadOnlyList<Achado> achados)
    {
        ArgumentNullException.ThrowIfNull(resumo);
        ArgumentNullException.ThrowIfNull(arquivos);
        ArgumentNullException.ThrowIfNull(achados);

        var linhas = new List<string>
        {
            string.Format(
                PtBr,
                "Resumo do sistema: {0}: {1} e {2}. Resultado: {3}.",
                Plural(resumo.Recebidos, "arquivo recebido", "arquivos recebidos"),
                Plural(resumo.Analisados, "analisado", "analisados"),
                Plural(resumo.NaoAnalisados, "não analisado", "não analisados"),
                resultado switch
                {
                    ResultadoGeral.SemInconsistencias => "sem inconsistências nas validações realizadas",
                    ResultadoGeral.ComInconsistencias => "com inconsistências",
                    _ => "inconclusivo, com revisão humana necessária",
                }),
        };

        var inconsistencias = achados.Where(a => a.Categoria is CategoriaAchado.ErroEstrutural or CategoriaAchado.InconsistenciaNegocio).ToList();
        if (inconsistencias.Count > 0)
        {
            linhas.Add($"{Plural(inconsistencias.Count, "Inconsistência", "Inconsistências", contagemNoInicio: false)} ({inconsistencias.Count.ToString(PtBr)}):");
            linhas.AddRange(inconsistencias.Take(MaximoInconsistenciasNoResumo).Select(a => "- " + Inconsistencia(a)));
            if (inconsistencias.Count > MaximoInconsistenciasNoResumo)
            {
                var restantes = inconsistencias.Count - MaximoInconsistenciasNoResumo;
                linhas.Add($"- … e mais {Plural(restantes, "inconsistência", "inconsistências")}, listadas no parecer.");
            }
        }

        var naoAnalisados = arquivos.Where(a => a.ValidacaoLayout.Situacao != SituacaoValidacao.Realizada).ToList();
        if (naoAnalisados.Count > 0)
        {
            linhas.Add("Arquivos não analisados:");
            linhas.AddRange(naoAnalisados.Select(a => $"- {a.Nome}: {Motivo(a.ValidacaoLayout.Motivo)}"));
        }

        var alertas = achados
            .Where(a => a.Categoria is CategoriaAchado.FalhaTecnica or CategoriaAchado.AusenciaEvidencias)
            .Select(a => a.Descricao)
            .ToList();
        if (!alertas.Any(a => a.StartsWith(CodigoErro.ProvedorIaIndisponivel, StringComparison.Ordinal)
            || a.StartsWith(CodigoErro.RespostaIaInvalida, StringComparison.Ordinal)))
        {
            alertas.Add("A análise por IA não está disponível nesta execução.");
        }

        linhas.Add("Alertas:");
        linhas.AddRange(alertas.Select(a => "- " + Alerta(a)));
        return string.Join("\n", linhas);
    }

    public const int MaximoInconsistenciasNoResumo = 10;

    private static string Inconsistencia(Achado achado)
    {
        var arquivo = achado.Arquivo is null ? string.Empty : $"{achado.Arquivo}: ";
        if (achado.Campo is not null && achado.ValorInformado is not null && achado.ValorEsperado is not null)
        {
            var item = achado.Item is { } numero ? $" (item {numero.ToString(PtBr)})" : string.Empty;
            var regra = achado.RegraId is null ? string.Empty : $" — regra {achado.RegraId}";
            return $"{arquivo}`{achado.Campo}`{item} informado {achado.ValorInformado}, esperado {achado.ValorEsperado}{regra}.";
        }

        return arquivo + Limitar(achado.Descricao);
    }

    private static string Motivo(string? motivo) =>
        motivo is null ? "motivo não registrado." : Limitar(motivo.Replace("Validação não realizada: ", string.Empty, StringComparison.Ordinal));

    private static string Alerta(string descricao)
    {
        var texto = Limitar(descricao);
        return texto.StartsWith(CodigoErro.ProvedorIaIndisponivel, StringComparison.Ordinal)
            || texto.StartsWith(CodigoErro.RespostaIaInvalida, StringComparison.Ordinal)
                ? $"Análise por IA não disponível ({texto})"
                : texto;
    }

    private static string Limitar(string texto) => texto.Length <= 300 ? texto : string.Concat(texto.AsSpan(0, 297), "...");

    private static string Plural(int quantidade, string singular, string plural, bool contagemNoInicio = true) =>
        contagemNoInicio
            ? $"{quantidade.ToString(PtBr)} {(quantidade == 1 ? singular : plural)}"
            : quantidade == 1 ? singular : plural;
}
