using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Domain.Documentos;

/// <summary>
/// Detecta documentos repetidos por chave de acesso e SHA-256 do conteúdo, no lote e contra a base
/// da sessão (FR-009). Mesma chave e mesmo hash: duplicado, analisado uma vez. Mesma chave e hash
/// diferente: conteúdo divergente, nenhum arquivo descartado.
/// </summary>
public sealed class DetectorDuplicidade
{
    private readonly IReadOnlyDictionary<string, string> _hashesDaSessao;
    private readonly Dictionary<string, (string Arquivo, string Hash)> _lote = new(StringComparer.Ordinal);

    /// <param name="hashesDaSessao">Chave de acesso → SHA-256 dos documentos já na sessão.</param>
    public DetectorDuplicidade(IReadOnlyDictionary<string, string>? hashesDaSessao = null) =>
        _hashesDaSessao = hashesDaSessao ?? new Dictionary<string, string>(StringComparer.Ordinal);

    public ResultadoDuplicidade Registrar(string arquivo, string chaveAcesso, string hashSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arquivo);
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveAcesso);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashSha256);

        if (_lote.TryGetValue(chaveAcesso, out var anterior))
        {
            return Comparar(anterior.Hash, hashSha256, anterior.Arquivo);
        }

        _lote[chaveAcesso] = (arquivo, hashSha256);
        return _hashesDaSessao.TryGetValue(chaveAcesso, out var hashSessao)
            ? Comparar(hashSessao, hashSha256, null)
            : new ResultadoDuplicidade(TipoDuplicidade.Nenhuma, null, null, true);
    }

    private static ResultadoDuplicidade Comparar(string hashAnterior, string hash, string? arquivoAnterior) =>
        string.Equals(hashAnterior, hash, StringComparison.OrdinalIgnoreCase)
            ? new ResultadoDuplicidade(TipoDuplicidade.Duplicado, arquivoAnterior, Severidade.Informativa, false)
            : new ResultadoDuplicidade(TipoDuplicidade.ConteudoDivergente, arquivoAnterior, Severidade.Alta, true);
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
