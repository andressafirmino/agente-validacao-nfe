using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.RAG.Servicos;

/// <summary>Catálogo em memória de trechos sintéticos para desenvolvimento (ingestão vetorial na evolução).</summary>
public sealed class CatalogoTrechos
{
    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IReadOnlyList<TrechoCatalogo> _trechos;

    public CatalogoTrechos(IHostEnvironment ambiente)
    {
        var caminho = Path.Combine(ambiente.ContentRootPath, "Dados", "trechos-semente.v1.json");
        if (!File.Exists(caminho))
        {
            caminho = Path.Combine(AppContext.BaseDirectory, "Dados", "trechos-semente.v1.json");
        }

        using var fluxo = File.OpenRead(caminho);
        var arquivo = JsonSerializer.Deserialize<ArquivoTrechos>(fluxo, OpcoesJson)
            ?? throw new InvalidOperationException("Catálogo de trechos vazio ou inválido.");
        _trechos = arquivo.Trechos ?? [];
    }

    public RespostaBusca Buscar(ParametrosBusca parametros)
    {
        var data = DateOnly.ParseExact(parametros.DataReferencia, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var candidatos = _trechos
            .Where(t => t.TipoDocumento == parametros.TipoDocumento
                && t.Modelo == parametros.Modelo
                && t.VersaoLayout == parametros.VersaoLayout
                && Vigente(t, data))
            .Select(t => (Trecho: t, Pontuacao: Pontuar(t, parametros)))
            .Where(t => t.Pontuacao > 0)
            .OrderByDescending(t => t.Pontuacao)
            .Take(Math.Clamp(parametros.QuantidadeMaxima, 1, 20))
            .Select(t => new TrechoResposta(
                t.Trecho.ChunkId,
                t.Trecho.Documento,
                t.Trecho.Versao,
                new VigenciaResposta(t.Trecho.Vigencia.Inicio, t.Trecho.Vigencia.Fim),
                t.Trecho.Localizacao is null
                    ? null
                    : new LocalizacaoResposta(t.Trecho.Localizacao.Secao, t.Trecho.Localizacao.Pagina),
                t.Trecho.Trecho,
                t.Pontuacao))
            .ToList();

        return new RespostaBusca("0.1", candidatos);
    }

    private static bool Vigente(TrechoCatalogo trecho, DateOnly data)
    {
        if (DateOnly.ParseExact(trecho.Vigencia.Inicio, "yyyy-MM-dd", CultureInfo.InvariantCulture) > data)
        {
            return false;
        }

        if (trecho.Vigencia.Fim is { } fim
            && DateOnly.ParseExact(fim, "yyyy-MM-dd", CultureInfo.InvariantCulture) < data)
        {
            return false;
        }

        return true;
    }

    private static double Pontuar(TrechoCatalogo trecho, ParametrosBusca parametros)
    {
        double pontuacao = 0;
        if (parametros.RegrasRelacionadas is { Length: > 0 } regras && trecho.RegrasRelacionadas is { Length: > 0 } doTrecho)
        {
            pontuacao += regras.Count(r => doTrecho.Contains(r, StringComparer.Ordinal)) * 2.0;
        }

        var consulta = parametros.Consulta.ToLowerInvariant();
        foreach (var palavra in trecho.PalavrasChave ?? [])
        {
            if (consulta.Contains(palavra, StringComparison.OrdinalIgnoreCase))
            {
                pontuacao += 1.0;
            }
        }

        if (consulta.Contains(trecho.Documento, StringComparison.OrdinalIgnoreCase))
        {
            pontuacao += 0.5;
        }

        return pontuacao;
    }

    private sealed record ArquivoTrechos(string? Versao, List<TrechoCatalogo>? Trechos);

    private sealed record TrechoCatalogo(
        string ChunkId,
        string Documento,
        string Versao,
        VigenciaCatalogo Vigencia,
        LocalizacaoCatalogo? Localizacao,
        string Trecho,
        string[]? RegrasRelacionadas,
        string TipoDocumento,
        string Modelo,
        string VersaoLayout,
        string[]? PalavrasChave);

    private sealed record VigenciaCatalogo(string Inicio, string? Fim);

    private sealed record LocalizacaoCatalogo(string? Secao, int? Pagina);
}

public sealed record ParametrosBusca(
    string TipoDocumento,
    string Modelo,
    string VersaoLayout,
    string DataReferencia,
    string Consulta,
    string[]? RegrasRelacionadas,
    int QuantidadeMaxima);

public sealed record RespostaBusca(
    [property: JsonPropertyName("versaoContrato")] string VersaoContrato,
    [property: JsonPropertyName("trechos")] IReadOnlyList<TrechoResposta> Trechos);

public sealed record TrechoResposta(
    [property: JsonPropertyName("chunkId")] string ChunkId,
    [property: JsonPropertyName("documento")] string Documento,
    [property: JsonPropertyName("versao")] string Versao,
    [property: JsonPropertyName("vigencia")] VigenciaResposta Vigencia,
    [property: JsonPropertyName("localizacao")] LocalizacaoResposta? Localizacao,
    [property: JsonPropertyName("trecho")] string Trecho,
    [property: JsonPropertyName("pontuacao")] double Pontuacao);

public sealed record VigenciaResposta(
    [property: JsonPropertyName("inicio")] string Inicio,
    [property: JsonPropertyName("fim")] string? Fim);

public sealed record LocalizacaoResposta(
    [property: JsonPropertyName("secao")] string? Secao,
    [property: JsonPropertyName("pagina")] int? Pagina);
