using System.Globalization;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Domain.Rastreabilidade;
using AuditoriaNFe.Domain.Sessoes;

namespace AuditoriaNFe.Application.Servicos;

/// <summary>Monta a resposta validada da pergunta para persistência e envelope (FR-026).</summary>
public static class MontadorResposta
{
    public static RespostaPerguntaExecucao De(ContratoIaV1 saida)
    {
        ArgumentNullException.ThrowIfNull(saida);
        if (saida.Resposta is not { } resposta)
        {
            throw new InvalidOperationException("A saída não contém resposta.");
        }

        var valores = resposta.ValoresCitados
            .Select(v => new ValorCitado(
                v.Valor,
                string.IsNullOrWhiteSpace(v.CalculoId) ? null : Guid.Parse(v.CalculoId!),
                v.ChaveAcesso,
                v.Caminho))
            .ToList();
        var excluidos = resposta.DocumentosExcluidos
            .Select(d => new DocumentoExcluidoResposta(d.ChaveAcesso, d.Motivo))
            .ToList();

        return new RespostaPerguntaExecucao(
            resposta.Texto,
            valores,
            excluidos,
            saida.InformacaoIndisponivel,
            saida.ForaDeEscopo);
    }

    public static RespostaPerguntaV1 ParaContrato(RespostaPerguntaExecucao resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        return new RespostaPerguntaV1(
            resposta.Texto,
            resposta.ValoresCitados.Select(v => new ValorCitadoV1(
                v.Valor,
                v.CalculoId,
                v.ChaveAcesso,
                v.Caminho)).ToList(),
            resposta.DocumentosExcluidos.Select(d => new DocumentoExcluidoV1(d.ChaveAcesso, d.Motivo)).ToList(),
            resposta.InformacaoIndisponivel ? true : null,
            resposta.ForaDeEscopo ? true : null);
    }
}
