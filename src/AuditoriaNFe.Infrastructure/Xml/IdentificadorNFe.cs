using System.Xml.Linq;
using AuditoriaNFe.Domain.Documentos;

namespace AuditoriaNFe.Infrastructure.Xml;

/// <summary>
/// Lê do XML a raiz, o namespace, a <c>versao</c>, o <c>ide/mod</c>, o <c>Id</c> de <c>infNFe</c>, o
/// protocolo, o <c>dhEmi</c> e o <c>dhRecbto</c>, como no documento, e avalia o suporte (FR-007,
/// FR-007a, FR-008).
/// </summary>
public static class IdentificadorNFe
{
    public static IdentificacaoDocumento Identificar(XDocument documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var raiz = documento.Root ?? throw new ArgumentException("Documento sem elemento raiz.", nameof(documento));
        var ns = raiz.Name.Namespace;

        var nfe = raiz.Name.LocalName == "nfeProc" ? raiz.Element(ns + "NFe") : raiz;
        var infNFe = nfe?.Element(ns + "infNFe");
        var ide = infNFe?.Element(ns + "ide");
        var infProt = raiz.Name.LocalName == "nfeProc" ? raiz.Element(ns + "protNFe")?.Element(ns + "infProt") : null;

        return IdentificacaoDocumento.Avaliar(
            raiz.Name.LocalName,
            ns.NamespaceName,
            (string?)infNFe?.Attribute("versao") ?? (string?)raiz.Attribute("versao"),
            ide?.Element(ns + "mod")?.Value,
            (string?)infNFe?.Attribute("Id"),
            infProt is not null,
            ide?.Element(ns + "dhEmi")?.Value,
            infProt?.Element(ns + "dhRecbto")?.Value);
    }

    /// <summary>Elemento <c>infNFe</c> de uma <c>NFe</c> ou <c>nfeProc</c>.</summary>
    public static XElement? InfNFe(XDocument documento)
    {
        var raiz = documento.Root;
        if (raiz is null)
        {
            return null;
        }

        var ns = raiz.Name.Namespace;
        var nfe = raiz.Name.LocalName == "nfeProc" ? raiz.Element(ns + "NFe") : raiz;
        return nfe?.Element(ns + "infNFe");
    }
}
