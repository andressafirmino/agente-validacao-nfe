using System.Xml.Schema;
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
    public ProvedorPacotesSchema(string diretorio) => throw new NotImplementedException("T072");

    public int VersaoManifesto => throw new NotImplementedException("T072");

    public IReadOnlyList<VigenciaPacote> Vigencias => throw new NotImplementedException("T072");

    /// <summary>Schemas compilados do pacote, com <c>TNFe</c> e <c>TNfeProc</c>.</summary>
    public XmlSchemaSet Esquemas(string pacoteId) => throw new NotImplementedException("T072");
}
