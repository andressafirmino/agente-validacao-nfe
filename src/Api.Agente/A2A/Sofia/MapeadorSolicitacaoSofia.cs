using System.Text;
using AuditoriaNFe.Application.CasosDeUso;
using AuditoriaNFe.Application.Fluxo;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Sessoes;
using Microsoft.Extensions.Options;

namespace Api.Agente.A2A.Sofia;

public sealed class MapeadorSolicitacaoSofia(IOptions<A2AOpcoes> a2a)
{
    public (SolicitacaoAuditoria Solicitacao, DadosSofia Dados) ParaCasoDeUso(SolicitacaoAnaliseSofia sofia)
    {
        if (!IdentificacaoSolicitante.TentarCriarPorNome(sofia.Usuario.Nome, a2a.Value.ChamadorPadrao, out var identificacao))
        {
            throw new InvalidOperationException("Nome de usuário inválido.");
        }

        var arquivos = sofia.Documentos
            .Select(d => new ArquivoSolicitacao(d.Nome, Encoding.UTF8.GetBytes(d.Texto), d.IdDocumento))
            .ToList();

        var chave = Execucao.MontarChaveIdempotencia(identificacao.ChamadorId, sofia.IdSolicitacao);
        var dados = new DadosSofia(sofia.IdExecucao, sofia.IdSolicitacao, chave);
        var solicitacao = new SolicitacaoAuditoria(
            identificacao,
            CanalExecucao.A2A,
            sofia.Pergunta.Trim(),
            arquivos,
            CorrelacaoId: sofia.IdSolicitacao.ToString("D"),
            Sofia: dados);

        return (solicitacao, dados);
    }
}

