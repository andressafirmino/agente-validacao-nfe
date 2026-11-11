using System.Diagnostics;
using System.Text.Json;
using Api.Agente.A2A.Sofia;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.CasosDeUso;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Sessoes;
using global::A2A;
using Microsoft.Extensions.Options;

namespace Api.Agente.A2A;

public sealed class ManipuladorSofiaA2A(
    ExecutarSolicitacaoAuditoria casoDeUso,
    ValidadorSolicitacaoSofia validador,
    MapeadorSolicitacaoSofia mapeadorEntrada,
    MapeadorResultadoSofia mapeadorSaida,
    IExecucaoRepositorio execucoes,
    IOptions<A2AOpcoes> a2a,
    ILogger<ManipuladorSofiaA2A> logger) : IAgentHandler
{
    public async Task ExecuteAsync(RequestContext context, AgentEventQueue queue, CancellationToken cancellationToken)
    {
        var updater = new TaskUpdater(queue, context.TaskId, context.ContextId);
        await updater.SubmitAsync(cancellationToken: cancellationToken);
        await updater.StartWorkAsync(context.Message, cancellationToken: cancellationToken);

        var partes = context.Message.Parts;
        if (partes.Count != 1 || partes[0].ContentCase != PartContentCase.Text || string.IsNullOrWhiteSpace(partes[0].Text))
        {
            await updater.FailAsync(MensagemErro(CodigoErro.MensagemForaDoContrato), cancellationToken: cancellationToken);
            return;
        }

        var texto = partes[0].Text!;
        if (!ValidadorSolicitacaoSofia.TentarDesserializar(texto, out var solicitacao, out _))
        {
            await updater.FailAsync(MensagemErro(CodigoErro.MensagemForaDoContrato), cancellationToken: cancellationToken);
            return;
        }

        if (solicitacao!.IdExecucao == Guid.Empty || solicitacao.IdSolicitacao == Guid.Empty)
        {
            await updater.FailAsync(MensagemErro(CodigoErro.MensagemForaDoContrato), cancellationToken: cancellationToken);
            return;
        }

        using var atividade = Activity.Current;
        atividade?.SetTag("idAgente", a2a.Value.IdAgente);
        atividade?.SetTag("idExecucao", solicitacao.IdExecucao);
        atividade?.SetTag("idSolicitacao", solicitacao.IdSolicitacao);

        var validacao = validador.Validar(solicitacao);
        if (!validacao.Valida)
        {
            var resultado = mapeadorSaida.DeEnvelope(
                MontadorEnvelope.Rejeitada(
                    Guid.Empty,
                    mapeadorEntrada.ParaCasoDeUso(solicitacao).Solicitacao.Identificacao,
                    CanalExecucao.A2A,
                    DateTimeOffset.UtcNow,
                    validacao.CodigoErro!,
                    validacao.Mensagem,
                    correlacaoId: solicitacao.IdSolicitacao.ToString("D")),
                solicitacao.IdExecucao,
                solicitacao.IdSolicitacao);
            await PublicarResultadoAsync(updater, resultado, cancellationToken);
            await updater.CompleteAsync(cancellationToken: cancellationToken);
            return;
        }

        var (auditoria, _) = mapeadorEntrada.ParaCasoDeUso(solicitacao);
        var inicio = Stopwatch.GetTimestamp();
        var execucao = await casoDeUso.ExecutarSofiaAsync(auditoria, cancellationToken);
        var duracao = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;

        ResultadoAnaliseSofia saida;
        if (execucao.Rejeitada)
        {
            saida = mapeadorSaida.DeEnvelope(execucao.Envelope, solicitacao.IdExecucao, solicitacao.IdSolicitacao,
                incluirIdInterno: execucao.Envelope.Erro?.Codigo != CodigoErro.SolicitacaoEmAndamento);
        }
        else
        {
            var entidade = await execucoes.ObterAsync(execucao.Envelope.ExecucaoId, auditoria.Identificacao.ChaveIsolamento, cancellationToken)
                ?? throw new InvalidOperationException("Execução não encontrada após processamento.");
            saida = mapeadorSaida.DeExecucao(entidade);
            atividade?.SetTag("execucaoId", entidade.Id);
        }

        logger.LogInformation(
            "A2A concluído em {DuracaoMs} ms com status {StatusSofia} para o agente {IdAgente}.",
            duracao,
            saida.Status,
            a2a.Value.IdAgente);

        await PublicarResultadoAsync(updater, saida, cancellationToken);
        await updater.CompleteAsync(cancellationToken: cancellationToken);
    }

    public Task CancelAsync(RequestContext context, AgentEventQueue queue, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    private static async Task PublicarResultadoAsync(TaskUpdater updater, ResultadoAnaliseSofia resultado, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(resultado, ContratoSofiaV1.Json);
        var artifact = new[] { Part.FromText(json) };
        await updater.AddArtifactAsync(
            artifact,
            name: ContratoSofiaV1.NomeArtifact,
            cancellationToken: cancellationToken);
    }

    private static Message MensagemErro(string codigo) =>
        new() { Parts = [Part.FromText($"{codigo}: {MensagensErro.Para(codigo)}")] };
}
