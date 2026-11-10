namespace AuditoriaNFe.Application.ContextProviders;

/// <summary>
/// Contexto da execução entregue ao agente interno, já minimizado (ADR-0004): dados extraídos
/// necessários, resultados determinísticos, estado das etapas, pacote e data de referência por
/// documento e trechos de manual recuperados. Nunca contém o XML.
/// </summary>
public sealed record ContextoExecucaoIa(
    IReadOnlyList<DocumentoContextoIa> Documentos,
    IReadOnlyList<AchadoContextoIa> Achados,
    IReadOnlyList<RegraContextoIa> Regras,
    IReadOnlyList<EtapaContextoIa> Etapas,
    IReadOnlyList<TrechoContextoIa> Trechos,
    string? Pergunta);

public sealed record DocumentoContextoIa(
    string Arquivo,
    string? ChaveAcesso,
    DateOnly? DataReferencia,
    string? PacoteAplicado,
    string ValidacaoLayout,
    string ValidacaoConteudo,
    IReadOnlyList<CampoContextoIa> Campos);

public sealed record CampoContextoIa(string Caminho, string Valor);

public sealed record AchadoContextoIa(
    Guid Id,
    string Categoria,
    string Severidade,
    string Descricao,
    string? Arquivo,
    string? Campo,
    string? ValorInformado,
    string? ValorEsperado,
    string? RegraId);

public sealed record RegraContextoIa(string RegraId, int VersaoRegra, string Descricao);

public sealed record EtapaContextoIa(int Numero, string Nome, string Estado);

public sealed record TrechoContextoIa(string ChunkId, string Documento, string Versao, string Trecho);
