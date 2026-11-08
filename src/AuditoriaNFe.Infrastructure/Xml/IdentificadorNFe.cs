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
    public static IdentificacaoDocumento Identificar(XDocument documento) => throw new NotImplementedException("T071");
}
