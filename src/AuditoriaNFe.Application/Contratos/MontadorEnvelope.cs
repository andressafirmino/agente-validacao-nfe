using System.Text.Json;
using AuditoriaNFe.Application.Servicos;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Application.Contratos;

/// <summary>Envelope v1 de uma execução gravada ou de uma solicitação rejeitada antes do processamento.</summary>
public static class MontadorEnvelope
{
    /// <summary>Só a partir do que foi gravado: a criação e a consulta devolvem o mesmo envelope.</summary>
    public static EnvelopeV1 De(Execucao execucao)
    {
        ArgumentNullException.ThrowIfNull(execucao);
        var fim = execucao.ConcluidaEm ?? execucao.IniciadaEm;
        var duracao = (long)Math.Max(0, (fim - execucao.IniciadaEm).TotalMilliseconds);
        return EnvelopeV1.De(execucao, fim, duracao) with
        {
            Parecer = execucao.Parecer is null ? null : MontadorParecer.ParaContrato(execucao),
            Resposta = execucao.RespostaPergunta is null ? null : MontadorResposta.ParaContrato(execucao.RespostaPergunta),
        };
    }

    /// <summary>
    /// Solicitação rejeitada (ERR-001 a ERR-003, ERR-005 a ERR-007, ERR-024, ERR-025): a etapa 1
    /// falha e as demais não são executadas; sem parecer.
    /// </summary>
    public static EnvelopeV1 Rejeitada(
        Guid execucaoId,
        IdentificacaoSolicitante? identificacao,
        CanalExecucao canal,
        DateTimeOffset agora,
        string codigo,
        string? mensagem = null,
        IReadOnlyDictionary<string, JsonElement>? detalhes = null,
        Guid? sessaoId = null,
        string? correlacaoId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        var texto = mensagem ?? MensagensErro.Para(codigo);
        var etapas = EtapaExecucao.Nomes
            .Select((nome, indice) => indice == 0
                ? new EtapaV1(1, nome, nameof(EstadoEtapa.Falhou), $"Solicitação rejeitada com {codigo}.", agora, agora)
                : new EtapaV1(indice + 1, nome, nameof(EstadoEtapa.NaoExecutada), $"Não executada: a solicitação foi rejeitada com {codigo}.", null, null))
            .ToList();

        return new EnvelopeV1
        {
            ExecucaoId = execucaoId,
            SessaoId = sessaoId,
            CorrelacaoId = correlacaoId,
            Estado = nameof(EstadoExecucao.Rejeitada),
            DataHora = agora,
            Canal = canal.ToString(),
            UsuarioIdentificado = identificacao?.UsuarioIdentificado ?? false,
            Etapas = etapas,
            Erro = new ErroV1(codigo, texto, null, detalhes),
            Metadados = new MetadadosV1(null, 0, null, null),
        };
    }
}
