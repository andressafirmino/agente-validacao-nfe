using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using System.Xml.Schema;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Schemas;
using AuditoriaNFe.Infrastructure.Comum;

namespace AuditoriaNFe.Infrastructure.Schemas;

/// <summary>
/// Carrega <c>manifesto.json</c> e valida os pacotes (data-model §5): SHA-256 de cada XSD, nenhum
/// arquivo fora do manifesto, vigências sem sobreposição nem lacuna e só o último com fim aberto.
/// Falha com <see cref="CargaInvalidaException"/>. Os schemas são compilados sob demanda e mantidos
/// em cache por pacote (research R-14).
/// </summary>
public sealed class ProvedorPacotesSchema
{
    public const string NomeManifesto = "manifesto.json";

    private readonly string _diretorio;
    private readonly Dictionary<string, PacoteManifesto> _pacotes;
    private readonly ConcurrentDictionary<string, Lazy<XmlSchemaSet>> _cache = new(StringComparer.Ordinal);

    public ProvedorPacotesSchema(string diretorio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diretorio);
        _diretorio = Path.GetFullPath(diretorio);
        var caminhoManifesto = Path.Combine(_diretorio, NomeManifesto);
        if (!File.Exists(caminhoManifesto))
        {
            throw new CargaInvalidaException($"Manifesto de schemas ausente em {diretorio}.");
        }

        Manifesto manifesto;
        try
        {
            manifesto = JsonSerializer.Deserialize<Manifesto>(File.ReadAllText(caminhoManifesto), OpcoesJson)
                ?? throw new CargaInvalidaException("Manifesto de schemas vazio.");
        }
        catch (JsonException ex)
        {
            throw new CargaInvalidaException($"Manifesto de schemas inválido: {ex.Message}");
        }

        var pacotes = manifesto.Pacotes ?? [];
        if (pacotes.Count == 0)
        {
            throw new CargaInvalidaException("Manifesto de schemas sem pacotes.");
        }

        VersaoManifesto = manifesto.Versao;
        var ordenados = pacotes.OrderBy(p => p.Ordem).ToList();
        ValidarVigencias(ordenados);
        ValidarArquivos(ordenados);

        _pacotes = ordenados.ToDictionary(p => p.Id!, StringComparer.Ordinal);
        Vigencias = [.. ordenados.Select(p => new VigenciaPacote(p.Ordem, p.Id!, Data(p.VigenciaInicio)!.Value, Data(p.VigenciaFim), Data(p.FimConvivencia)))];
    }

    public int VersaoManifesto { get; }

    public IReadOnlyList<VigenciaPacote> Vigencias { get; }

    /// <summary>Schemas compilados do pacote, com <c>TNFe</c> e <c>TNfeProc</c>.</summary>
    public XmlSchemaSet Esquemas(string pacoteId)
    {
        ArgumentNullException.ThrowIfNull(pacoteId);
        if (!_pacotes.TryGetValue(pacoteId, out var pacote))
        {
            throw new ArgumentException($"Pacote de schema desconhecido: {pacoteId}.", nameof(pacoteId));
        }

        return _cache.GetOrAdd(pacoteId, _ => new Lazy<XmlSchemaSet>(() => Compilar(pacote))).Value;
    }

    private XmlSchemaSet Compilar(PacoteManifesto pacote)
    {
        var pasta = Path.Combine(_diretorio, pacote.Id!);
        var resolvedor = new ResolvedorRestritoPacote(pasta);
        var conjunto = new XmlSchemaSet { XmlResolver = resolvedor };
        foreach (var schema in new[] { pacote.SchemaNFe!, pacote.SchemaNFeProc! })
        {
            using var leitor = XmlReader.Create(
                Path.Combine(pasta, schema),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = resolvedor });
            conjunto.Add(null, leitor);
        }

        conjunto.Compile();
        foreach (var tipo in new[] { "TNFe", "TNfeProc" })
        {
            if (!conjunto.GlobalTypes.Contains(new XmlQualifiedName(tipo, IdentificacaoDocumento.NamespaceNFe)))
            {
                throw new CargaInvalidaException($"Pacote {pacote.Id} sem o tipo {tipo}.");
            }
        }

        return conjunto;
    }

    private static void ValidarVigencias(List<PacoteManifesto> pacotes)
    {
        if (pacotes.Any(p => string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.SchemaNFe) || string.IsNullOrWhiteSpace(p.SchemaNFeProc)))
        {
            throw new CargaInvalidaException("Pacote de schema sem id ou sem schemas no manifesto.");
        }

        if (pacotes.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != pacotes.Count
            || pacotes.Select(p => p.Ordem).Distinct().Count() != pacotes.Count)
        {
            throw new CargaInvalidaException("Pacotes de schema com id ou ordem repetidos.");
        }

        for (var i = 0; i < pacotes.Count; i++)
        {
            var pacote = pacotes[i];
            var inicio = Data(pacote.VigenciaInicio) ?? throw new CargaInvalidaException($"Pacote {pacote.Id} sem início de vigência.");
            var fim = Data(pacote.VigenciaFim);
            var ultimo = i == pacotes.Count - 1;
            if (fim is null && !ultimo)
            {
                throw new CargaInvalidaException($"Só o último pacote pode ter fim de vigência aberto ({pacote.Id}).");
            }

            if (fim is { } f && f < inicio)
            {
                throw new CargaInvalidaException($"Pacote {pacote.Id} com fim de vigência anterior ao início.");
            }

            if (!ultimo)
            {
                var proximoInicio = Data(pacotes[i + 1].VigenciaInicio);
                if (proximoInicio is null || proximoInicio.Value <= fim!.Value)
                {
                    throw new CargaInvalidaException($"Vigências sobrepostas entre {pacote.Id} e {pacotes[i + 1].Id}.");
                }

                if (proximoInicio.Value != fim.Value.AddDays(1))
                {
                    throw new CargaInvalidaException($"Lacuna de vigência entre {pacote.Id} e {pacotes[i + 1].Id}.");
                }
            }
        }
    }

    private void ValidarArquivos(List<PacoteManifesto> pacotes)
    {
        var pastasEsperadas = pacotes.Select(p => p.Id!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pasta in Directory.EnumerateDirectories(_diretorio))
        {
            if (!pastasEsperadas.Contains(Path.GetFileName(pasta)))
            {
                throw new CargaInvalidaException($"Pasta fora do manifesto de schemas: {Path.GetFileName(pasta)}.");
            }
        }

        foreach (var arquivo in Directory.EnumerateFiles(_diretorio))
        {
            if (!string.Equals(Path.GetFileName(arquivo), NomeManifesto, StringComparison.OrdinalIgnoreCase))
            {
                throw new CargaInvalidaException($"Arquivo fora do manifesto de schemas: {Path.GetFileName(arquivo)}.");
            }
        }

        foreach (var pacote in pacotes)
        {
            var pasta = Path.Combine(_diretorio, pacote.Id!);
            if (!Directory.Exists(pasta))
            {
                throw new CargaInvalidaException($"Pasta do pacote {pacote.Id} ausente.");
            }

            var arquivos = pacote.Arquivos ?? [];
            var esperados = arquivos.Select(a => a.Nome ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!esperados.Contains(pacote.SchemaNFe!) || !esperados.Contains(pacote.SchemaNFeProc!))
            {
                throw new CargaInvalidaException($"Pacote {pacote.Id} sem {pacote.SchemaNFe} ou {pacote.SchemaNFeProc} na lista de arquivos.");
            }

            foreach (var existente in Directory.EnumerateFileSystemEntries(pasta))
            {
                if (!esperados.Contains(Path.GetFileName(existente)) || Directory.Exists(existente))
                {
                    throw new CargaInvalidaException($"Arquivo fora do manifesto no pacote {pacote.Id}: {Path.GetFileName(existente)}.");
                }
            }

            foreach (var arquivo in arquivos)
            {
                var caminho = Path.Combine(pasta, arquivo.Nome!);
                if (!File.Exists(caminho))
                {
                    throw new CargaInvalidaException($"XSD ausente no pacote {pacote.Id}: {arquivo.Nome}.");
                }

                var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(caminho)));
                if (!string.Equals(hash, arquivo.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new CargaInvalidaException($"SHA-256 divergente no pacote {pacote.Id}: {arquivo.Nome}.");
                }
            }
        }
    }

    private static DateOnly? Data(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? null
            : DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
                ? data
                : throw new CargaInvalidaException($"Data inválida no manifesto de schemas: {valor}.");

    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web);

    private sealed record Manifesto(int Versao, List<PacoteManifesto>? Pacotes);

    private sealed record PacoteManifesto(
        int Ordem,
        string? Id,
        string? VigenciaInicio,
        string? VigenciaFim,
        string? FimConvivencia,
        string? SchemaNFe,
        string? SchemaNFeProc,
        List<ArquivoManifesto>? Arquivos);

    private sealed record ArquivoManifesto(string? Nome, string? Sha256);
}
