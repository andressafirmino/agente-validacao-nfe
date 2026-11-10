using System.Net;
using System.Xml;

namespace AuditoriaNFe.Infrastructure.Schemas;

/// <summary>
/// Resolvedor dos <c>import</c> e <c>include</c> dos XSDs que só abre arquivos locais dentro da pasta
/// do pacote (research R-14). Qualquer outro endereço falha.
/// </summary>
public sealed class ResolvedorRestritoPacote : XmlResolver
{
    private readonly string _diretorio;

    public ResolvedorRestritoPacote(string diretorio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diretorio);
        _diretorio = Path.TrimEndingDirectorySeparator(Path.GetFullPath(diretorio)) + Path.DirectorySeparatorChar;
    }

    public override ICredentials Credentials
    {
        set { }
    }

    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        ArgumentNullException.ThrowIfNull(absoluteUri);
        if (!absoluteUri.IsFile)
        {
            throw new XmlException($"Referência fora do pacote de schema: {absoluteUri.Scheme}.");
        }

        var caminho = Path.GetFullPath(absoluteUri.LocalPath);
        if (!caminho.StartsWith(_diretorio, StringComparison.OrdinalIgnoreCase) || !File.Exists(caminho))
        {
            throw new XmlException($"Referência fora do pacote de schema: {Path.GetFileName(caminho)}.");
        }

        return File.OpenRead(caminho);
    }
}
