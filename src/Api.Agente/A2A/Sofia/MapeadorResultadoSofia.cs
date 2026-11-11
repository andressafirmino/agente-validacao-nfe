using System.Globalization;
using System.Text;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Parecer;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Domain.Validacao;
using Microsoft.Extensions.Options;

namespace Api.Agente.A2A.Sofia;

public sealed class MapeadorResultadoSofia(IOptions<A2AOpcoes> a2a)
{
    public ResultadoAnaliseSofia DeExecucao(Execucao execucao, bool incluirIdInterno = true) =>
        Montar(
            execucao.IdExecucaoSofia ?? Guid.Empty,
            execucao.IdSolicitacaoSofia ?? Guid.Empty,
            DeterminarStatus(execucao),
            TextoAnalise(execucao),
            MapearAchados(execucao),
            MontarObservacoes(execucao, incluirIdInterno),
            MontarErro(execucao));

    public ResultadoAnaliseSofia DeEnvelope(EnvelopeV1 envelope, Guid idExecucao, Guid idSolicitacao, bool incluirIdInterno = false)
    {
        var status = "erro";
        var analise = envelope.Erro?.Mensagem ?? MensagensErro.Para(CodigoErro.MensagemForaDoContrato);
        var erro = envelope.Erro is { } e ? new ErroSofia(e.Codigo, e.Mensagem) : new ErroSofia(CodigoErro.MensagemForaDoContrato, analise);
        return Montar(idExecucao, idSolicitacao, status, analise, [], incluirIdInterno ? [$"Identificador interno da execução: {envelope.ExecucaoId}."] : null, erro);
    }

    private ResultadoAnaliseSofia Montar(
        Guid idExecucao,
        Guid idSolicitacao,
        string status,
        string analise,
        IReadOnlyList<AchadoSofia> achados,
        IReadOnlyList<string>? observacoes,
        ErroSofia? erro) =>
        new(
            ContratoSofiaV1.Versao,
            idExecucao,
            idSolicitacao,
            new AgenteSofia(a2a.Value.IdAgente, "AgenteAuditoriaNFe"),
            status,
            analise,
            achados,
            observacoes,
            status == "erro" ? erro : null);

    private static string DeterminarStatus(Execucao execucao) =>
        execucao.Estado is EstadoExecucao.Rejeitada or EstadoExecucao.Falhou
            ? "erro"
            : execucao.Parecer is { } p && p.ResultadoGeral == ResultadoGeral.Inconclusiva
                ? "parcial"
                : execucao.Arquivos.Any(a => a.ValidacaoLayout.Situacao != SituacaoValidacao.Realizada
                    || a.ValidacaoConteudo.Situacao != SituacaoValidacao.Realizada)
                    ? "parcial"
                    : "concluido";

    private static string TextoAnalise(Execucao execucao)
    {
        if (execucao.Estado is EstadoExecucao.Rejeitada or EstadoExecucao.Falhou)
        {
            return execucao.Erro?.Mensagem ?? MensagensErro.Para(CodigoErro.MensagemForaDoContrato);
        }

        return string.IsNullOrWhiteSpace(execucao.Parecer?.ResumoTextual)
            ? "Análise concluída."
            : execucao.Parecer.ResumoTextual;
    }

    private static ErroSofia? MontarErro(Execucao execucao) =>
        execucao.Estado is EstadoExecucao.Rejeitada or EstadoExecucao.Falhou && execucao.Erro is { } e
            ? new ErroSofia(e.Codigo, e.Mensagem)
            : null;

    private static IReadOnlyList<AchadoSofia> MapearAchados(Execucao execucao)
    {
        if (execucao.Parecer is null)
        {
            return [];
        }

        var porNome = execucao.Arquivos.ToDictionary(a => a.Nome, a => a.IdDocumento, StringComparer.Ordinal);
        return execucao.Parecer.Achados.Select(a => new AchadoSofia(
            a.Id.ToString("D"),
            $"{RotuloCategoria(a.Categoria)}: {a.Descricao}",
            IdDocumentos(a, porNome),
            Evidencias(a),
            MapearSeveridade(a.Severidade))).ToList();
    }

    private static IReadOnlyList<string> IdDocumentos(Achado achado, IReadOnlyDictionary<string, string?> porNome) =>
        achado.Arquivo is { } nome && porNome.TryGetValue(nome, out var id) && id is not null
            ? [id]
            : [];

    private static IReadOnlyList<string> Evidencias(Achado achado) =>
        achado.Evidencias.Select(MontarEvidencia).Where(s => s.Length > 0).ToList();

    private static string MontarEvidencia(Evidencia evidencia) =>
        evidencia.Tipo switch
        {
            TipoEvidencia.CampoXml => $"Campo {evidencia.Caminho}: valor informado {evidencia.Valor}",
            TipoEvidencia.Calculo => $"Cálculo {evidencia.CalculoId:D} registrado na execução.",
            TipoEvidencia.RegraCatalogo => $"Regra {evidencia.RegraId} do catálogo de regras {evidencia.VersaoCatalogo} (versão {evidencia.VersaoRegra})",
            TipoEvidencia.TrechoManual => $"Trecho de manual {evidencia.Documento} v{evidencia.Versao}: {evidencia.Trecho ?? string.Empty}".TrimEnd(),
            _ => string.Empty,
        };

    private static string? MapearSeveridade(Severidade severidade) =>
        severidade switch
        {
            Severidade.Alta => "alta",
            Severidade.Media => "media",
            Severidade.Baixa => "baixa",
            Severidade.Informativa => null,
            _ => null,
        };

    private static string RotuloCategoria(CategoriaAchado categoria) =>
        categoria switch
        {
            CategoriaAchado.ErroEstrutural => "Erro estrutural do documento",
            CategoriaAchado.InconsistenciaNegocio => "Inconsistência de negócio",
            CategoriaAchado.IndicioIa => "Indício",
            CategoriaAchado.AusenciaEvidencias => "Ausência de evidências",
            _ => "Achado",
        };

    private static IReadOnlyList<string> MontarObservacoes(Execucao execucao, bool incluirIdInterno)
    {
        var linhas = new List<string>();
        foreach (var arquivo in execucao.Arquivos)
        {
            linhas.Add(LinhaDocumento(arquivo));
        }

        foreach (var lim in execucao.Parecer?.Limitacoes ?? [])
        {
            linhas.Add(lim);
        }

        foreach (var val in execucao.Parecer?.ValidacoesNaoRealizadas ?? [])
        {
            linhas.Add($"{val.Arquivo} — {val.Validacao}: {val.Motivo}");
        }

        if (execucao.Parecer?.RevisaoHumana is { Necessaria: true } revisao)
        {
            linhas.Add($"Revisão humana recomendada: {string.Join("; ", revisao.Motivos)}.");
        }

        if (incluirIdInterno)
        {
            linhas.Add($"Identificador interno da execução: {execucao.Id}.");
        }

        return linhas;
    }

    private static string LinhaDocumento(ArquivoRecebido arquivo)
    {
        var id = arquivo.IdDocumento is null ? string.Empty : $"{arquivo.IdDocumento} ";
        if (arquivo.ValidacaoLayout.Situacao == SituacaoValidacao.Realizada)
        {
            var layout = arquivo.ValidacaoLayout;
            var convivencia = layout.EmConvivencia ? " (convivência)" : string.Empty;
            return $"{id}({arquivo.Nome}): NF-e 4.00 validada com o pacote {layout.PacoteAplicado} (data de referência {layout.DataReferencia:yyyy-MM-dd}, origem {layout.OrigemDataReferencia}){convivencia}.";
        }

        var motivo = arquivo.ValidacaoLayout.Motivo ?? arquivo.ValidacaoConteudo.ToString();
        return $"{id}({arquivo.Nome}): não analisado — {motivo}.";
    }
}
