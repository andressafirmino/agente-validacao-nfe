using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.ContextProviders;
using AuditoriaNFe.Application.Consumo;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Application.Ferramentas;
using AuditoriaNFe.Domain.Calculos;
using AuditoriaNFe.Domain.Rastreabilidade;
using AuditoriaNFe.Domain.Sessoes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Application.Servicos;

/// <summary>
/// Validação estrutural e semântica da saída do agente interno (P08, data-model §15): cada
/// evidência <c>CampoXml</c> existe nos dados entregues, <c>RegraCatalogo</c> no catálogo e
/// <c>TrechoManual</c> no conjunto recuperado; cada achado citado existe. A saída inválida gera nova
/// tentativa com mensagem de correção até <c>Ia:MaxTentativas</c>; ao esgotar, ERR-015.
/// </summary>
public sealed partial class ValidadorSaidaIa(IOptions<IaOpcoes> ia, ILogger<ValidadorSaidaIa> logger)
{
    public const string MensagemInicial =
        "Analise os documentos do contexto da execução e responda somente no formato do contrato ContratoIaV1.";

    private static readonly HashSet<string> Severidades = new(StringComparer.Ordinal) { "Alta", "Media", "Baixa", "Informativa" };
    private static readonly HashSet<string> TiposEvidencia = new(StringComparer.Ordinal) { "CampoXml", "RegraCatalogo", "TrechoManual", "Calculo" };

    /// <summary>
    /// Saída validada do agente. <paramref name="historicoAgente"/> é a <c>AgentSession</c> serializada
    /// da sessão (research R-08); o resultado traz a sessão atualizada para ser gravada.
    /// </summary>
    public async Task<ResultadoAnaliseIa> ObterSaidaAsync(
        AIAgent agente,
        ContextoAgenteIa contexto,
        string? historicoAgente,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agente);
        ArgumentNullException.ThrowIfNull(contexto);

        var escopo = EscopoConsumo.Atual;
        var sessao = await RestaurarSessaoAsync(agente, historicoAgente, escopo?.ExecucaoId, cancellationToken).ConfigureAwait(false);
        var resultado = await ExecutarTentativasAsync(agente, sessao, contexto, escopo, cancellationToken).ConfigureAwait(false);
        return resultado with { HistoricoAgente = await SerializarSessaoAsync(agente, sessao, escopo?.ExecucaoId, cancellationToken).ConfigureAwait(false) };
    }

    /// <summary>Mensagem de entrada: o pedido de análise e, quando houver, a pergunta delimitada.</summary>
    public static string MensagemDeEntrada(ContextoExecucaoIa contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return string.IsNullOrWhiteSpace(contexto.Pergunta)
            ? MensagemInicial
            : MensagemInicial + Environment.NewLine + ContextoExecucaoProvider.MontarPergunta(contexto.Pergunta);
    }

    private async Task<ResultadoAnaliseIa> ExecutarTentativasAsync(
        AIAgent agente,
        AgentSession sessao,
        ContextoAgenteIa contexto,
        EscopoConsumo? escopo,
        CancellationToken cancellationToken)
    {
        var mensagem = MensagemDeEntrada(contexto.Execucao);
        string? modelo = null;
        var maximo = ia.Value.MaxTentativas;
        for (var tentativa = 1; tentativa <= maximo; tentativa++)
        {
            escopo?.DefinirTentativa(tentativa);
            AgentResponse<ContratoIaV1> resposta;
            try
            {
                resposta = await agente.RunAsync<ContratoIaV1>(mensagem, sessao, ContratoIaV1.OpcoesJson, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ProvedorIaIndisponivelException)
            {
                return ResultadoAnaliseIa.ProvedorIndisponivel(modelo, tentativa);
            }

            modelo = (resposta.RawRepresentation as ChatResponse)?.ModelId ?? modelo;

            IReadOnlyList<string> problemas = Ler(resposta, out var saida);
            if (saida is not null)
            {
                var calculos = EscopoFerramentasExecucao.Atual?.Execucao.Calculos ?? contexto.Calculos;
                problemas = Validar(saida, contexto with { Calculos = calculos });
            }

            if (problemas.Count == 0)
            {
                return ResultadoAnaliseIa.Valida(saida!, modelo, tentativa);
            }

            LogSaidaInvalida(logger, escopo?.ExecucaoId, tentativa, problemas.Count);
            mensagem = "A resposta anterior foi rejeitada pela validação do sistema e descartada. Problemas: "
                + string.Join(" ", problemas)
                + " Responda novamente, somente no formato do contrato ContratoIaV1 e só com evidências presentes no contexto.";
        }

        return ResultadoAnaliseIa.RespostaInvalida(modelo, maximo);
    }

    /// <summary>Histórico ilegível (por exemplo, de outra versão) não impede a execução: começa outro.</summary>
    private async Task<AgentSession> RestaurarSessaoAsync(AIAgent agente, string? historicoAgente, Guid? execucaoId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(historicoAgente))
        {
            try
            {
                using var documento = JsonDocument.Parse(historicoAgente);
                return await agente.DeserializeSessionAsync(documento.RootElement.Clone(), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception erro) when (erro is JsonException or InvalidOperationException or NotSupportedException or ArgumentException)
            {
                LogHistoricoIlegivel(logger, execucaoId, erro.GetType().Name);
            }
        }

        return await agente.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> SerializarSessaoAsync(AIAgent agente, AgentSession sessao, Guid? execucaoId, CancellationToken cancellationToken)
    {
        try
        {
            var serializada = await agente.SerializeSessionAsync(sessao, cancellationToken: cancellationToken).ConfigureAwait(false);
            return serializada.GetRawText();
        }
        catch (Exception erro) when (erro is JsonException or InvalidOperationException or NotSupportedException)
        {
            LogHistoricoIlegivel(logger, execucaoId, erro.GetType().Name);
            return null;
        }
    }

    /// <summary>Problemas da saída; lista vazia quando a saída é válida.</summary>
    public static IReadOnlyList<string> Validar(ContratoIaV1 saida, ContextoAgenteIa contexto)
    {
        ArgumentNullException.ThrowIfNull(saida);
        ArgumentNullException.ThrowIfNull(contexto);

        var problemas = new List<string>();
        if (saida.VersaoContrato != ContratoIa.Versao)
        {
            problemas.Add($"versaoContrato deve ser \"{ContratoIa.Versao}\".");
        }

        if (string.IsNullOrWhiteSpace(saida.Resumo))
        {
            problemas.Add("resumo é obrigatório.");
        }

        var campos = contexto.Documentos
            .Where(d => d.ChaveAcesso is not null)
            .GroupBy(d => d.ChaveAcesso!, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(d => d.Campos).GroupBy(c => c.Caminho, StringComparer.Ordinal).ToDictionary(c => c.Key, c => c.First().Valor, StringComparer.Ordinal),
                StringComparer.Ordinal);
        var regras = contexto.Regras.Select(r => r.RegraId).ToHashSet(StringComparer.Ordinal);
        var trechos = contexto.Execucao.Trechos.Select(t => t.ChunkId).ToHashSet(StringComparer.Ordinal);
        var achados = contexto.Execucao.Achados.Select(a => a.Id).ToHashSet();
        var calculosPorId = contexto.Calculos.ToDictionary(c => c.Id);

        for (var i = 0; i < saida.Indicios.Count; i++)
        {
            var indicio = saida.Indicios[i];
            var rotulo = $"indicios[{i}]";
            if (string.IsNullOrWhiteSpace(indicio.Descricao))
            {
                problemas.Add($"{rotulo}.descricao é obrigatória.");
            }

            if (!Severidades.Contains(indicio.Severidade))
            {
                problemas.Add($"{rotulo}.severidade inválida.");
            }

            if (indicio.Evidencias.Count == 0)
            {
                problemas.Add($"{rotulo} exige ao menos uma evidência.");
            }

            for (var j = 0; j < indicio.Evidencias.Count; j++)
            {
                ValidarEvidencia(indicio.Evidencias[j], $"{rotulo}.evidencias[{j}]", campos, regras, trechos, calculosPorId, problemas);
            }

            ValidarAchados(indicio.AchadosRelacionados, $"{rotulo}.achadosRelacionados", achados, problemas);
        }

        for (var i = 0; i < saida.AusenciasEvidencia.Count; i++)
        {
            var ausencia = saida.AusenciasEvidencia[i];
            if (string.IsNullOrWhiteSpace(ausencia.PontoAvaliado) || string.IsNullOrWhiteSpace(ausencia.Motivo))
            {
                problemas.Add($"ausenciasEvidencia[{i}] exige pontoAvaliado e motivo.");
            }
        }

        for (var i = 0; i < saida.Recomendacoes.Count; i++)
        {
            var recomendacao = saida.Recomendacoes[i];
            if (string.IsNullOrWhiteSpace(recomendacao.Texto) || recomendacao.AchadosVinculados.Count == 0)
            {
                problemas.Add($"recomendacoes[{i}] exige texto e ao menos um achado vinculado.");
            }

            ValidarAchados(recomendacao.AchadosVinculados, $"recomendacoes[{i}].achadosVinculados", achados, problemas);
            ValidarValores(recomendacao.ValoresCitados, $"recomendacoes[{i}].valoresCitados", problemas);
        }

        if (contexto.Tipo == TipoExecucao.Pergunta)
        {
            problemas.AddRange(ValidarRespostaPergunta(saida, campos, calculosPorId));
        }
        else if (contexto.Tipo == TipoExecucao.AnaliseComPergunta)
        {
            if (saida.Resposta is not null || saida.ForaDeEscopo || saida.InformacaoIndisponivel)
            {
                problemas.AddRange(ValidarRespostaPergunta(saida, campos, calculosPorId));
            }
        }
        else
        {
            if (saida.Resposta is not null)
            {
                problemas.Add("resposta deve ser null quando não há pergunta.");
            }

            if (saida.ForaDeEscopo || saida.InformacaoIndisponivel)
            {
                problemas.Add("foraDeEscopo e informacaoIndisponivel devem ser false em análise de lote.");
            }
        }

        return problemas;
    }

    private static IEnumerable<string> ValidarRespostaPergunta(
        ContratoIaV1 saida,
        Dictionary<string, Dictionary<string, string>> campos,
        IReadOnlyDictionary<Guid, RegistroCalculo> calculosPorId)
    {
        var problemas = new List<string>();
        if (saida.ForaDeEscopo && saida.InformacaoIndisponivel)
        {
            problemas.Add("foraDeEscopo e informacaoIndisponivel não podem ser true juntos.");
        }

        if (saida.Resposta is not { } resposta)
        {
            problemas.Add("resposta é obrigatória quando há pergunta.");
            return problemas;
        }

        if (string.IsNullOrWhiteSpace(resposta.Texto))
        {
            problemas.Add("resposta.texto é obrigatório.");
            return problemas;
        }

        if (saida.ForaDeEscopo || saida.InformacaoIndisponivel)
        {
            if (resposta.ValoresCitados.Count > 0 && saida.InformacaoIndisponivel)
            {
                problemas.Add("informacaoIndisponivel não pode citar valores financeiros.");
            }

            return problemas;
        }

        var valores = resposta.ValoresCitados
            .Select(v => new ValorCitado(
                v.Valor,
                string.IsNullOrWhiteSpace(v.CalculoId) ? null : Guid.TryParse(v.CalculoId, out var id) ? id : null,
                v.ChaveAcesso,
                v.Caminho))
            .ToList();
        var camposLeitura = campos.ToDictionary(
            e => e.Key,
            e => (IReadOnlyDictionary<string, string>)e.Value,
            StringComparer.Ordinal);
        problemas.AddRange(VerificadorValoresCitados.Verificar(resposta.Texto, valores, calculosPorId, camposLeitura));
        ValidarDocumentosExcluidos(resposta, calculosPorId, problemas);
        return problemas;
    }

    private static void ValidarDocumentosExcluidos(
        RespostaIaV1 resposta,
        IReadOnlyDictionary<Guid, RegistroCalculo> calculos,
        List<string> problemas)
    {
        var idsCalculo = resposta.ValoresCitados
            .Select(v => v.CalculoId)
            .Where(id => !string.IsNullOrWhiteSpace(id) && Guid.TryParse(id, out _))
            .Select(id => Guid.Parse(id!))
            .Distinct()
            .ToList();
        if (idsCalculo.Count == 0)
        {
            return;
        }

        var esperados = idsCalculo
            .SelectMany(id => calculos.TryGetValue(id, out var registro) ? registro.DocumentosExcluidos : [])
            .Select(d => (d.ChaveAcesso, Motivo: d.Motivo.ToString()))
            .ToHashSet();
        var informados = resposta.DocumentosExcluidos.Select(d => (d.ChaveAcesso, d.Motivo)).ToHashSet();
        if (!esperados.SetEquals(informados))
        {
            problemas.Add("resposta.documentosExcluidos não confere com os cálculos citados.");
        }
    }

    private static List<string> Ler(AgentResponse<ContratoIaV1> resposta, out ContratoIaV1? saida)
    {
        try
        {
            saida = resposta.Result;
            return saida is null ? ["A resposta está vazia."] : [];
        }
        catch (Exception erro) when (erro is JsonException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            saida = null;
            return ["A resposta não é um JSON válido do contrato ContratoIaV1."];
        }
    }

    private static void ValidarEvidencia(
        EvidenciaIaV1 evidencia,
        string rotulo,
        Dictionary<string, Dictionary<string, string>> campos,
        HashSet<string> regras,
        HashSet<string> trechos,
        IReadOnlyDictionary<Guid, RegistroCalculo> calculos,
        List<string> problemas)
    {
        if (!TiposEvidencia.Contains(evidencia.Tipo))
        {
            problemas.Add($"{rotulo}.tipo inválido.");
            return;
        }

        switch (evidencia.Tipo)
        {
            case "CampoXml":
                if (evidencia.ChaveAcesso is null || evidencia.Caminho is null || evidencia.Valor is null
                    || !campos.TryGetValue(evidencia.ChaveAcesso, out var documento)
                    || !documento.TryGetValue(evidencia.Caminho, out var valor)
                    || !MesmoValor(valor, evidencia.Valor))
                {
                    problemas.Add($"{rotulo}: o campo citado não existe nos dados extraídos com esse valor.");
                }

                break;
            case "RegraCatalogo":
                if (evidencia.RegraId is null || !regras.Contains(evidencia.RegraId))
                {
                    problemas.Add($"{rotulo}: a regra citada não existe no catálogo aplicado.");
                }

                break;
            case "TrechoManual":
                if (evidencia.ChunkId is null || !trechos.Contains(evidencia.ChunkId))
                {
                    problemas.Add($"{rotulo}: o trecho citado não pertence ao conjunto recuperado.");
                }

                break;
            case "Calculo":
                if (evidencia.CalculoId is null || !Guid.TryParse(evidencia.CalculoId, out var id) || !calculos.ContainsKey(id))
                {
                    problemas.Add($"{rotulo}: o cálculo citado não existe na execução.");
                }

                break;
        }
    }

    private static void ValidarAchados(IReadOnlyList<string> ids, string rotulo, HashSet<Guid> achados, List<string> problemas)
    {
        if (ids.Any(id => !Guid.TryParse(id, out var guid) || !achados.Contains(guid)))
        {
            problemas.Add($"{rotulo}: há achado citado que não existe na execução.");
        }
    }

    private static void ValidarValores(IReadOnlyList<ValorCitadoIaV1> valores, string rotulo, List<string> problemas)
    {
        if (valores.Any(v => !Decimal().IsMatch(v.Valor)))
        {
            problemas.Add($"{rotulo}: valor fora do formato decimal.");
        }
    }

    private static bool MesmoValor(string extraido, string citado) =>
        string.Equals(extraido, citado, StringComparison.Ordinal)
        || (decimal.TryParse(extraido, NumberStyles.Number, CultureInfo.InvariantCulture, out var a)
            && decimal.TryParse(citado, NumberStyles.Number, CultureInfo.InvariantCulture, out var b)
            && a == b);

    [GeneratedRegex(@"^-?[0-9]+(\.[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Decimal();

    [LoggerMessage(EventId = 1511, Level = LogLevel.Warning,
        Message = "Saída do agente rejeitada na execução {ExecucaoId}, tentativa {Tentativa}: {QuantidadeProblemas} problema(s).")]
    private static partial void LogSaidaInvalida(ILogger logger, Guid? execucaoId, int tentativa, int quantidadeProblemas);

    [LoggerMessage(EventId = 1512, Level = LogLevel.Warning,
        Message = "Histórico da sessão do agente ilegível na execução {ExecucaoId} ({TipoErro}); a sessão do agente recomeça.")]
    private static partial void LogHistoricoIlegivel(ILogger logger, Guid? execucaoId, string tipoErro);
}

public enum SituacaoAnaliseIa
{
    Valida,
    RespostaInvalida,
    ProvedorIndisponivel,
}

public sealed record ResultadoAnaliseIa(SituacaoAnaliseIa Situacao, ContratoIaV1? Saida, string? Modelo, int Tentativas)
{
    /// <summary><c>AgentSession</c> serializada depois das tentativas, para <c>historicoAgente</c>.</summary>
    public string? HistoricoAgente { get; init; }

    public static ResultadoAnaliseIa Valida(ContratoIaV1 saida, string? modelo, int tentativas) => new(SituacaoAnaliseIa.Valida, saida, modelo, tentativas);

    public static ResultadoAnaliseIa RespostaInvalida(string? modelo, int tentativas) => new(SituacaoAnaliseIa.RespostaInvalida, null, modelo, tentativas);

    public static ResultadoAnaliseIa ProvedorIndisponivel(string? modelo, int tentativas) => new(SituacaoAnaliseIa.ProvedorIndisponivel, null, modelo, tentativas);
}
