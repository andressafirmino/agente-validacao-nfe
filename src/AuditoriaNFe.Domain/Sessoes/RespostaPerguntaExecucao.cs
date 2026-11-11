using AuditoriaNFe.Domain.Rastreabilidade;

namespace AuditoriaNFe.Domain.Sessoes;

/// <summary>Resposta validada a uma pergunta (data-model §15 e envelope <c>RespostaPergunta</c>).</summary>
public sealed class RespostaPerguntaExecucao
{
    public RespostaPerguntaExecucao(
        string texto,
        IReadOnlyList<ValorCitado> valoresCitados,
        IReadOnlyList<DocumentoExcluidoResposta> documentosExcluidos,
        bool informacaoIndisponivel,
        bool foraDeEscopo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);
        ArgumentNullException.ThrowIfNull(valoresCitados);
        ArgumentNullException.ThrowIfNull(documentosExcluidos);
        Texto = texto;
        ValoresCitados = valoresCitados;
        DocumentosExcluidos = documentosExcluidos;
        InformacaoIndisponivel = informacaoIndisponivel;
        ForaDeEscopo = foraDeEscopo;
    }

    public string Texto { get; }
    public IReadOnlyList<ValorCitado> ValoresCitados { get; }
    public IReadOnlyList<DocumentoExcluidoResposta> DocumentosExcluidos { get; }
    public bool InformacaoIndisponivel { get; }
    public bool ForaDeEscopo { get; }
}

public sealed record DocumentoExcluidoResposta(string ChaveAcesso, string Motivo);
