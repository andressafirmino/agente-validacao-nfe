namespace AuditoriaNFe.Application.Abstracoes;

/// <summary>
/// Consulta a manuais oficiais da etapa 5 (contracts/consulta-manuais-mcp.md, FR-047a a FR-047d).
/// Falha de transporte, timeout, ferramenta ausente ou resposta inválida lança
/// <see cref="ConsultaManuaisIndisponivelException"/> (ERR-013).
/// </summary>
public interface IConsultaManuais
{
    bool Habilitada { get; }

    Task<ResultadoConsultaManuais> ConsultarAsync(ConsultaManuais consulta, CancellationToken cancellationToken);
}

/// <summary>Entrada da ferramenta <c>buscar_trechos_manual</c>; nunca o XML nem dados pessoais.</summary>
public sealed record ConsultaManuais(
    string TipoDocumento,
    string Modelo,
    string VersaoLayout,
    DateOnly DataReferencia,
    string Consulta,
    IReadOnlyList<string> RegrasRelacionadas,
    int QuantidadeMaxima);

public sealed record ResultadoConsultaManuais(IReadOnlyList<TrechoManual> Trechos);

/// <summary>Trecho aceito: conteúdo não confiável, entregue à IA delimitado (P07).</summary>
public sealed record TrechoManual(
    string ChunkId,
    string Documento,
    string Versao,
    DateOnly VigenciaInicio,
    DateOnly? VigenciaFim,
    string Trecho,
    string? Secao = null,
    int? Pagina = null,
    double? Pontuacao = null);

public sealed class ConsultaManuaisIndisponivelException(string mensagem, Exception? interna = null)
    : Exception(mensagem, interna);
