using System.Globalization;
using System.Text.Json;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Agente;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Consumo;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Application.Fluxo;
using AuditoriaNFe.Application.Sessoes;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Domain.Validacao;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditoriaNFe.Application.CasosDeUso;

/// <summary>Ids da SOFIA e chave de idempotência (FR-043h), só no canal A2A.</summary>
public sealed record DadosSofia(Guid IdExecucao, Guid IdSolicitacao, string ChaveIdempotencia);

/// <summary>Solicitação recebida por um dos canais, já com a identificação lida.</summary>
public sealed record SolicitacaoAuditoria(
    IdentificacaoSolicitante Identificacao,
    CanalExecucao Canal,
    string? Pergunta,
    IReadOnlyList<ArquivoSolicitacao> Arquivos,
    Guid? SessaoId = null,
    string? CorrelacaoId = null,
    DadosSofia? Sofia = null);

/// <param name="Rejeitada">O status HTTP vem do código do erro do envelope.</param>
public sealed record ResultadoSolicitacao(EnvelopeV1 Envelope, bool Rejeitada);

/// <summary>
/// Caso de uso da solicitação de auditoria: limites e conteúdo mínimo antes de ler os XMLs (ERR-001
/// a ERR-003), sessão nova ou continuidade da sessão informada (ERR-005, ERR-006; FR-036 a FR-040),
/// etapa 1, fluxo das demais etapas com <c>Execucao:TempoMaximo</c> (ERR-016), com os novos
/// documentos incorporados à base da sessão e duplicidade conferida contra ela (FR-038, FR-009),
/// persistência da execução, dos documentos e do histórico do agente e armazenamento indisponível
/// como ERR-025, sem parecer parcial.
/// </summary>
public sealed partial class ExecutarSolicitacaoAuditoria(
    FluxoNoveEtapas fluxo,
    ServicoSessoes servicoSessoes,
    ISessaoRepositorio sessoes,
    IExecucaoRepositorio execucoes,
    IProcessadorXml xml,
    ICatalogoRegras catalogo,
    IConsultaManuais manuais,
    IRelogio relogio,
    IGeradorIds ids,
    IOptions<LimitesOpcoes> limites,
    IOptions<ExecucaoOpcoes> execucaoOpcoes,
    ILogger<ExecutarSolicitacaoAuditoria> logger)
{
    /// <summary>Canal A2A: idempotência (FR-043h) antes de criar execução.</summary>
    public async Task<ResultadoSolicitacao> ExecutarSofiaAsync(SolicitacaoAuditoria solicitacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        if (solicitacao.Sofia is not { } sofia)
        {
            throw new ArgumentException("Solicitação A2A exige dados da SOFIA.", nameof(solicitacao));
        }

        var existente = await execucoes.ObterPorChaveIdempotenciaSofiaAsync(sofia.ChaveIdempotencia, cancellationToken);
        if (existente is not null)
        {
            if (existente.IdExecucaoSofia != sofia.IdExecucao || existente.UsuarioId != solicitacao.Identificacao.UsuarioId)
            {
                var execucaoId = ids.Novo();
                return Rejeitar(solicitacao, execucaoId, CodigoErro.MensagemForaDoContrato);
            }

            switch (existente.Estado)
            {
                case EstadoExecucao.Recebida or EstadoExecucao.EmProcessamento:
                    return ResultadoErroSofia(solicitacao, sofia, CodigoErro.SolicitacaoEmAndamento);
                case EstadoExecucao.Concluida or EstadoExecucao.Rejeitada:
                    return new ResultadoSolicitacao(MontadorEnvelope.De(existente), false);
                case EstadoExecucao.Falhou:
                    break;
            }
        }

        try
        {
            return await ExecutarAsync(solicitacao, cancellationToken);
        }
        catch (ExecucaoIdempotenciaConcorrenteException)
        {
            existente = await execucoes.ObterPorChaveIdempotenciaSofiaAsync(sofia.ChaveIdempotencia, cancellationToken);
            if (existente is null)
            {
                throw;
            }

            return existente.Estado is EstadoExecucao.Recebida or EstadoExecucao.EmProcessamento
                ? ResultadoErroSofia(solicitacao, sofia, CodigoErro.SolicitacaoEmAndamento)
                : new ResultadoSolicitacao(MontadorEnvelope.De(existente), false);
        }
    }

    public async Task<ResultadoSolicitacao> ExecutarAsync(SolicitacaoAuditoria solicitacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        var execucaoId = ids.Novo();
        var recebidaEm = relogio.Agora;
        using var escopoLog = logger.BeginScope(new Dictionary<string, object?> { ["ExecucaoId"] = execucaoId });

        if (Rejeicao(solicitacao, execucaoId, recebidaEm) is { } rejeitada)
        {
            LogRejeitada(logger, execucaoId, rejeitada.Envelope.Erro!.Codigo);
            return rejeitada;
        }

        try
        {
            return await ProcessarAsync(solicitacao, execucaoId, recebidaEm, cancellationToken);
        }
        catch (ArmazenamentoIndisponivelException erro)
        {
            LogArmazenamentoIndisponivel(logger, execucaoId, erro.GetType().Name);
            return Rejeitar(solicitacao, execucaoId, CodigoErro.ArmazenamentoIndisponivel);
        }
    }

    /// <summary>Mensagem que o canal não conseguiu ler conforme o contrato (ERR-007), sem criar execução.</summary>
    public ResultadoSolicitacao RejeitarForaDoContrato(IdentificacaoSolicitante identificacao, CanalExecucao canal)
    {
        var execucaoId = ids.Novo();
        LogRejeitada(logger, execucaoId, CodigoErro.MensagemForaDoContrato);
        return Rejeitar(new SolicitacaoAuditoria(identificacao, canal, null, []), execucaoId, CodigoErro.MensagemForaDoContrato);
    }

    private async Task<ResultadoSolicitacao> ProcessarAsync(
        SolicitacaoAuditoria solicitacao,
        Guid execucaoId,
        DateTimeOffset recebidaEm,
        CancellationToken cancellationToken)
    {
        var identificacao = solicitacao.Identificacao;
        var obtencao = await servicoSessoes.ObterOuCriarAsync(solicitacao.SessaoId, identificacao, recebidaEm, cancellationToken);
        if (obtencao.Sessao is not { } sessao)
        {
            var codigo = obtencao.CodigoErro!;
            return Rejeitar(
                solicitacao,
                execucaoId,
                codigo,
                obtencao.Mensagem,
                sessaoId: codigo == CodigoErro.SessaoExpirada ? solicitacao.SessaoId : null);
        }

        var execucao = Execucao.Receber(
            execucaoId,
            identificacao,
            solicitacao.Canal,
            Tipo(solicitacao),
            recebidaEm,
            solicitacao.CorrelacaoId,
            solicitacao.Pergunta,
            manuais.Habilitada,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["envelope"] = EnvelopeV1.Versao,
                ["contratoIa"] = ContratoIa.Versao,
                ["manifestoSchemas"] = xml.VersaoManifesto,
                ["catalogoRegras"] = catalogo.VersaoCatalogoRegras,
                ["catalogoExtracao"] = catalogo.VersaoCatalogoExtracao,
                ["instrucoes"] = InstrucoesAgente.Versao,
            });
        if (solicitacao.Sofia is { } sofia)
        {
            execucao.ConfigurarSofia(
                sofia.IdExecucao,
                sofia.IdSolicitacao,
                solicitacao.Identificacao.UsuarioNome,
                sofia.ChaveIdempotencia);
        }

        execucao.AssociarSessao(sessao.Id);
        execucao.IniciarProcessamento();
        execucao.RegistrarEtapa(1, EstadoEtapa.Executada, recebidaEm, relogio.Agora);
        sessao.RegistrarAtividade(relogio.Agora);
        sessao.RegistrarExecucao(execucao.Id, relogio.Agora);
        await execucoes.InserirAsync(execucao, cancellationToken);

        var andamento = new AndamentoFluxo(execucao);
        using var tempoMaximo = new CancellationTokenSource(execucaoOpcoes.Value.TempoMaximo);
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, tempoMaximo.Token);
        using (var escopo = EscopoConsumo.Iniciar(
            execucao.Id,
            execucao.CorrelacaoId,
            execucao.ChaveIsolamento,
            solicitacao.Arquivos.Count > 0 ? "AnalisarNFe" : "ResponderPergunta"))
        {
            try
            {
                await fluxo.ExecutarAsync(andamento, sessao, solicitacao.Arquivos, limite.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !tempoMaximo.IsCancellationRequested)
            {
                var agora = relogio.Agora;
                execucao.Falhar(new ErroExecucao(CodigoErro.RequisicaoCancelada, MensagensErro.Para(CodigoErro.RequisicaoCancelada)), agora);
            }
            catch (OperationCanceledException) when (tempoMaximo.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                var agora = relogio.Agora;
                if (!execucao.Etapa(andamento.Etapa).Registrada)
                {
                    execucao.RegistrarEtapa(andamento.Etapa, EstadoEtapa.Falhou, andamento.InicioEtapa, agora, MensagensErro.Para(CodigoErro.TempoMaximoEsgotado));
                }

                execucao.Falhar(new ErroExecucao(CodigoErro.TempoMaximoEsgotado, MensagensErro.Para(CodigoErro.TempoMaximoEsgotado)), agora);
                LogTempoEsgotado(logger, execucao.Id, andamento.Etapa);
            }

            execucao.DefinirConsumo(escopo.Resumir());
        }

        await execucoes.AtualizarAsync(execucao, CancellationToken.None);
        await sessoes.AtualizarAsync(sessao, CancellationToken.None);
        LogConcluida(logger, execucao.Id, execucao.Estado.ToString(), execucao.Erro?.Codigo);
        return new ResultadoSolicitacao(MontadorEnvelope.De(execucao), false);
    }

    private ResultadoSolicitacao? Rejeicao(SolicitacaoAuditoria solicitacao, Guid execucaoId, DateTimeOffset agora)
    {
        if (solicitacao.CorrelacaoId is { Length: > Execucao.TamanhoMaximoCorrelacao })
        {
            return Rejeitar(solicitacao with { CorrelacaoId = null }, execucaoId, CodigoErro.MensagemForaDoContrato, agora: agora);
        }

        if (string.IsNullOrWhiteSpace(solicitacao.Pergunta) && solicitacao.Arquivos.Count == 0)
        {
            return Rejeitar(solicitacao, execucaoId, CodigoErro.SolicitacaoSemConteudo, agora: agora);
        }

        var opcoes = limites.Value;
        if (solicitacao.Arquivos.Count > opcoes.QuantidadeArquivos)
        {
            return Rejeitar(solicitacao, execucaoId, CodigoErro.QuantidadeArquivosExcedida, MensagensErro.QuantidadeArquivosExcedida(opcoes.QuantidadeArquivos), agora: agora);
        }

        var total = solicitacao.Arquivos.Sum(a => a.Conteudo.LongLength);
        if (total > opcoes.TamanhoTotalBytes)
        {
            var detalhes = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["limiteBytes"] = JsonSerializer.SerializeToElement(opcoes.TamanhoTotalBytes),
                ["totalRecebidoBytes"] = JsonSerializer.SerializeToElement(total),
            };
            return Rejeitar(solicitacao, execucaoId, CodigoErro.TamanhoExcedido, MensagensErro.TamanhoExcedido(opcoes.TamanhoTotalBytes, total), detalhes, agora: agora);
        }

        return null;
    }

    private ResultadoSolicitacao Rejeitar(
        SolicitacaoAuditoria solicitacao,
        Guid execucaoId,
        string codigo,
        string? mensagem = null,
        IReadOnlyDictionary<string, JsonElement>? detalhes = null,
        Guid? sessaoId = null,
        DateTimeOffset? agora = null) =>
        new(
            MontadorEnvelope.Rejeitada(
                execucaoId,
                solicitacao.Identificacao,
                solicitacao.Canal,
                agora ?? relogio.Agora,
                codigo,
                mensagem,
                detalhes,
                sessaoId,
                solicitacao.CorrelacaoId),
            true);

    private ResultadoSolicitacao ResultadoErroSofia(SolicitacaoAuditoria solicitacao, DadosSofia sofia, string codigo)
    {
        var execucaoId = ids.Novo();
        var envelope = MontadorEnvelope.Rejeitada(
            execucaoId,
            solicitacao.Identificacao,
            solicitacao.Canal,
            relogio.Agora,
            codigo,
            correlacaoId: sofia.IdSolicitacao.ToString("D"));
        return new ResultadoSolicitacao(envelope, true);
    }

    private static TipoExecucao Tipo(SolicitacaoAuditoria solicitacao) =>
        (solicitacao.Arquivos.Count > 0, !string.IsNullOrWhiteSpace(solicitacao.Pergunta)) switch
        {
            (true, true) => TipoExecucao.AnaliseComPergunta,
            (true, false) => TipoExecucao.AnaliseLote,
            _ => TipoExecucao.Pergunta,
        };

    [LoggerMessage(EventId = 1531, Level = LogLevel.Information,
        Message = "Solicitação rejeitada antes do processamento: execução {ExecucaoId}, {Codigo}.")]
    private static partial void LogRejeitada(ILogger logger, Guid execucaoId, string codigo);

    [LoggerMessage(EventId = 1532, Level = LogLevel.Information,
        Message = "Execução {ExecucaoId} terminou em {Estado} ({Codigo}).")]
    private static partial void LogConcluida(ILogger logger, Guid execucaoId, string estado, string? codigo);

    [LoggerMessage(EventId = 1533, Level = LogLevel.Warning,
        Message = "Tempo máximo esgotado na execução {ExecucaoId}, etapa {Etapa}.")]
    private static partial void LogTempoEsgotado(ILogger logger, Guid execucaoId, int etapa);

    [LoggerMessage(EventId = 1534, Level = LogLevel.Error,
        Message = "Armazenamento indisponível na execução {ExecucaoId} ({TipoErro}).")]
    private static partial void LogArmazenamentoIndisponivel(ILogger logger, Guid execucaoId, string tipoErro);
}
