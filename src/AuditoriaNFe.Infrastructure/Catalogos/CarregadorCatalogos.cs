using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuditoriaNFe.Domain.Regras;
using AuditoriaNFe.Domain.Validacao;
using AuditoriaNFe.Infrastructure.Comum;

namespace AuditoriaNFe.Infrastructure.Catalogos;

/// <summary>
/// Carrega e valida o catálogo de extração e o de regras (data-model §4 e §6). Regra sem fonte, sem
/// vigência, com implementação inexistente, com campo fora do catálogo de extração ou com versões
/// sobrepostas falha com <see cref="CargaInvalidaException"/>.
/// </summary>
/// <remarks>
/// Extração: <c>{ versaoCatalogo, campos: [{ caminho, tipo, classe }] }</c>, com <c>det[*]</c> nos
/// campos dos itens. Regras: <c>{ versaoCatalogo, aprovacao: { por, em }, fontes: [{ documento,
/// versao, sha256 }], regras: [{ id, versaoRegra, grupo, descricao, fonte: { documento, versao,
/// paginas }, vigencia: { inicio, fim }, severidade, tolerancia, aplicacaoSefaz: { tipo,
/// codigoRejeicao }, condicoes, implementacao }] }</c>.
/// </remarks>
public static class CarregadorCatalogos
{
    public static CatalogosCarregados Carregar(
        string caminhoRegras,
        string caminhoExtracao,
        IEnumerable<IImplementacaoRegra> implementacoes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoRegras);
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoExtracao);
        ArgumentNullException.ThrowIfNull(implementacoes);

        var extracao = Ler<CatalogoExtracaoJson>(caminhoExtracao, "extração");
        if (string.IsNullOrWhiteSpace(extracao.VersaoCatalogo) || extracao.Campos is not { Count: > 0 })
        {
            throw new CargaInvalidaException("Catálogo de extração sem versão ou sem campos.");
        }

        var campos = extracao.Campos.Select(c => new CampoCatalogoExtracao(
                Obrigatorio(c.Caminho, "caminho do campo de extração"),
                Obrigatorio(c.Tipo, $"tipo do campo {c.Caminho}"),
                Obrigatorio(c.Classe, $"classe do campo {c.Caminho}")))
            .ToList();
        var caminhos = campos.Select(c => c.Caminho).ToHashSet(StringComparer.Ordinal);
        if (caminhos.Count != campos.Count)
        {
            throw new CargaInvalidaException("Catálogo de extração com campo repetido.");
        }

        var porId = new Dictionary<string, IImplementacaoRegra>(StringComparer.Ordinal);
        foreach (var implementacao in implementacoes)
        {
            porId[implementacao.Id] = implementacao;
        }

        var catalogo = Ler<CatalogoRegrasJson>(caminhoRegras, "regras");
        if (string.IsNullOrWhiteSpace(catalogo.VersaoCatalogo) || catalogo.Regras is null)
        {
            throw new CargaInvalidaException("Catálogo de regras sem versão ou sem regras.");
        }

        if (catalogo.Aprovacao is null || string.IsNullOrWhiteSpace(catalogo.Aprovacao.Por) || string.IsNullOrWhiteSpace(catalogo.Aprovacao.Em))
        {
            throw new CargaInvalidaException("Catálogo de regras sem aprovação.");
        }

        if (catalogo.Fontes is not { Count: > 0 } || catalogo.Fontes.Any(f => string.IsNullOrWhiteSpace(f.Documento) || string.IsNullOrWhiteSpace(f.Sha256)))
        {
            throw new CargaInvalidaException("Catálogo de regras sem as fontes oficiais e seus SHA-256.");
        }

        var regras = catalogo.Regras.Select(r => Converter(r, porId, caminhos)).ToList();
        ValidarSobreposicao(regras);

        return new CatalogosCarregados(catalogo.VersaoCatalogo, regras, extracao.VersaoCatalogo, campos);
    }

    private static RegraConteudo Converter(RegraJson r, Dictionary<string, IImplementacaoRegra> implementacoes, HashSet<string> caminhos)
    {
        var id = Obrigatorio(r.Id, "id da regra");
        if (r.VersaoRegra is not > 0)
        {
            throw new CargaInvalidaException($"Regra {id} sem versão.");
        }

        if (r.Fonte is null || string.IsNullOrWhiteSpace(r.Fonte.Documento) || string.IsNullOrWhiteSpace(r.Fonte.Versao))
        {
            throw new CargaInvalidaException($"Regra {id} v{r.VersaoRegra} sem fonte oficial.");
        }

        if (r.Vigencia is null || Data(r.Vigencia.Inicio, id) is not { } inicio)
        {
            throw new CargaInvalidaException($"Regra {id} v{r.VersaoRegra} sem vigência.");
        }

        var fim = Data(r.Vigencia.Fim, id);
        if (fim < inicio)
        {
            throw new CargaInvalidaException($"Regra {id} v{r.VersaoRegra} com fim de vigência anterior ao início.");
        }

        var nomeImplementacao = Obrigatorio(r.Implementacao, $"implementação da regra {id}");
        if (!implementacoes.TryGetValue(nomeImplementacao, out var implementacao))
        {
            throw new CargaInvalidaException($"Regra {id} v{r.VersaoRegra} com implementação inexistente: {nomeImplementacao}.");
        }

        var foraDoCatalogo = implementacao.Campos.FirstOrDefault(c => !caminhos.Contains(c));
        if (foraDoCatalogo is not null)
        {
            throw new CargaInvalidaException($"Regra {id} v{r.VersaoRegra} usa campo fora do catálogo de extração: {foraDoCatalogo}.");
        }

        return new RegraConteudo
        {
            Id = id,
            VersaoRegra = r.VersaoRegra.Value,
            Grupo = Enum<GrupoRegra>(r.Grupo, id),
            Descricao = Obrigatorio(r.Descricao, $"descrição da regra {id}"),
            Fonte = new FonteRegra(r.Fonte.Documento, r.Fonte.Versao, r.Fonte.Paginas ?? string.Empty),
            VigenciaInicio = inicio,
            VigenciaFim = fim,
            Severidade = Enum<Severidade>(r.Severidade, id),
            Tolerancia = r.Tolerancia,
            AplicacaoSefaz = r.AplicacaoSefaz is null
                ? throw new CargaInvalidaException($"Regra {id} sem aplicação na SEFAZ.")
                : new AplicacaoSefaz(Enum<TipoAplicacaoSefaz>(r.AplicacaoSefaz.Tipo, id), r.AplicacaoSefaz.CodigoRejeicao ?? string.Empty),
            Condicoes = r.Condicoes ?? [],
            Implementacao = nomeImplementacao,
        };
    }

    private static void ValidarSobreposicao(List<RegraConteudo> regras)
    {
        foreach (var grupo in regras.GroupBy(r => r.Id, StringComparer.Ordinal))
        {
            var versoes = grupo.OrderBy(r => r.VigenciaInicio).ToList();
            if (versoes.Select(v => v.VersaoRegra).Distinct().Count() != versoes.Count)
            {
                throw new CargaInvalidaException($"Regra {grupo.Key} com versão repetida.");
            }

            for (var i = 1; i < versoes.Count; i++)
            {
                if (versoes[i - 1].VigenciaFim is not { } fimAnterior || versoes[i].VigenciaInicio <= fimAnterior)
                {
                    throw new CargaInvalidaException($"Regra {grupo.Key} com versões de vigência sobreposta.");
                }
            }
        }
    }

    private static T Ler<T>(string caminho, string nome)
    {
        if (!File.Exists(caminho))
        {
            throw new CargaInvalidaException($"Catálogo de {nome} ausente: {Path.GetFileName(caminho)}.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(caminho), OpcoesJson)
                ?? throw new CargaInvalidaException($"Catálogo de {nome} vazio.");
        }
        catch (JsonException ex)
        {
            throw new CargaInvalidaException($"Catálogo de {nome} inválido: {ex.Message}");
        }
    }

    private static string Obrigatorio(string? valor, string descricao) =>
        string.IsNullOrWhiteSpace(valor) ? throw new CargaInvalidaException($"Catálogo sem {descricao}.") : valor;

    private static DateOnly? Data(string? valor, string regra) =>
        string.IsNullOrWhiteSpace(valor)
            ? null
            : DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
                ? data
                : throw new CargaInvalidaException($"Regra {regra} com data inválida: {valor}.");

    private static TEnum Enum<TEnum>(string? valor, string regra)
        where TEnum : struct, Enum =>
        System.Enum.TryParse<TEnum>(valor, ignoreCase: false, out var resultado) && System.Enum.IsDefined(resultado)
            ? resultado
            : throw new CargaInvalidaException($"Regra {regra} com {typeof(TEnum).Name} inválido: {valor}.");

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict,
    };

    private sealed record CatalogoExtracaoJson(string? VersaoCatalogo, List<CampoJson>? Campos);

    private sealed record CampoJson(string? Caminho, string? Tipo, string? Classe);

    private sealed record CatalogoRegrasJson(string? VersaoCatalogo, AprovacaoJson? Aprovacao, List<FonteCatalogoJson>? Fontes, List<RegraJson>? Regras);

    private sealed record AprovacaoJson(string? Por, string? Em);

    private sealed record FonteCatalogoJson(string? Documento, string? Versao, string? Sha256);

    private sealed record RegraJson(
        string? Id,
        int? VersaoRegra,
        string? Grupo,
        string? Descricao,
        FonteRegraJson? Fonte,
        VigenciaJson? Vigencia,
        string? Severidade,
        decimal? Tolerancia,
        AplicacaoSefazJson? AplicacaoSefaz,
        List<string>? Condicoes,
        string? Implementacao);

    private sealed record FonteRegraJson(string? Documento, string? Versao, string? Paginas);

    private sealed record VigenciaJson(string? Inicio, string? Fim);

    private sealed record AplicacaoSefazJson(string? Tipo, string? CodigoRejeicao);
}

public sealed record CatalogosCarregados(
    string VersaoCatalogoRegras,
    IReadOnlyList<RegraConteudo> Regras,
    string VersaoCatalogoExtracao,
    IReadOnlyList<CampoCatalogoExtracao> CamposExtracao);

public sealed record CampoCatalogoExtracao(string Caminho, string Tipo, string Classe);
