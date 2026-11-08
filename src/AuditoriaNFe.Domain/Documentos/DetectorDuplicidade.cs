using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Domain.Documentos;

/// <summary>
/// Detecta documentos repetidos por chave de acesso e SHA-256 do conteúdo, no lote e contra a base
/// da sessão (FR-009). Mesma chave e mesmo hash: duplicado, analisado uma vez. Mesma chave e hash
/// diferente: conteúdo divergente, nenhum arquivo descartado.
/// </summary>
public sealed class DetectorDuplicidade
{
    /// <param name="hashesDaSessao">Chave de acesso → SHA-256 dos documentos já na sessão.</param>
    public DetectorDuplicidade(IReadOnlyDictionary<string, string>? hashesDaSessao = null) =>
        throw new NotImplementedException("T069");

    public ResultadoDuplicidade Registrar(string arquivo, string chaveAcesso, string hashSha256) =>
        throw new NotImplementedException("T069");
}

public enum TipoDuplicidade
{
    Nenhuma,

    /// <summary>Mesma chave e mesmo conteúdo (ERR-011).</summary>
    Duplicado,

    /// <summary>Mesma chave com conteúdo diferente (ERR-012).</summary>
    ConteudoDivergente,
}

/// <param name="ArquivoAnterior">Arquivo do lote com a mesma chave; nulo quando a repetição é contra a sessão.</param>
/// <param name="Severidade">Informativa no duplicado, Alta no conteúdo divergente, nula sem repetição.</param>
/// <param name="DeveAnalisar">Falso só no duplicado, que já foi analisado.</param>
public sealed record ResultadoDuplicidade(
    TipoDuplicidade Tipo,
    string? ArquivoAnterior,
    Severidade? Severidade,
    bool DeveAnalisar);
