using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace AuditoriaNFe.Tests.Fixtures.Xml;

/// <summary>Validação direta contra os XSDs de um pacote, só para conferir as fixtures nos testes.</summary>
internal static class ValidacaoXsdTeste
{
    public static readonly string DiretorioSchemas = Path.Combine(AppContext.BaseDirectory, "Schemas", "NFe");

    public static List<string> Validar(XDocument documento, string pacote, string schema)
    {
        var conjunto = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        using (var leitor = XmlReader.Create(
            Path.Combine(DiretorioSchemas, pacote, schema),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = new XmlUrlResolver() }))
        {
            conjunto.Add(null, leitor);
        }

        conjunto.Compile();
        var erros = new List<string>();
        documento.Validate(conjunto, (_, e) => erros.Add($"{e.Severity}: {e.Message}"));
        return erros;
    }
}
