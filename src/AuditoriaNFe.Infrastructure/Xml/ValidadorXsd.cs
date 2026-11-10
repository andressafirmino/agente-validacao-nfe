using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using AuditoriaNFe.Domain.Schemas;
using AuditoriaNFe.Infrastructure.Schemas;

namespace AuditoriaNFe.Infrastructure.Xml;

/// <summary>
/// Valida <c>NFe</c> contra <c>nfe_v4.00.xsd</c> e <c>nfeProc</c> contra <c>procNFe_v4.00.xsd</c> do
/// pacote vigente na data de referência, com nova tentativa no anterior na convivência (FR-010,
/// FR-010f, FR-011). O documento deve ter sido carregado com informação de linha.
/// </summary>
public sealed class ValidadorXsd
{
    private readonly ProvedorPacotesSchema _provedor;
    private readonly SeletorPacote _seletor;

    public ValidadorXsd(ProvedorPacotesSchema provedor)
    {
        ArgumentNullException.ThrowIfNull(provedor);
        _provedor = provedor;
        _seletor = new SeletorPacote(provedor.Vigencias);
    }

    public SeletorPacote Seletor => _seletor;

    public IReadOnlyList<ErroXsd> ValidarNoPacote(XDocument documento, string pacoteId)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var esquemas = _provedor.Esquemas(pacoteId);
        var erros = new List<ErroXsd>();
        lock (esquemas)
        {
            documento.Validate(esquemas, (origem, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                {
                    var linha = origem as IXmlLineInfo;
                    erros.Add(new ErroXsd(
                        e.Message,
                        e.Exception?.LineNumber is > 0 ? e.Exception.LineNumber : linha?.HasLineInfo() == true ? linha.LineNumber : null,
                        e.Exception?.LinePosition is > 0 ? e.Exception.LinePosition : linha?.HasLineInfo() == true ? linha.LinePosition : null,
                        Caminho(origem as XObject),
                        pacoteId));
                }
            });
        }

        return erros;
    }

    public ResultadoPacote<ErroXsd> Validar(XDocument documento, DateOnly dataReferencia, DateOnly hoje) =>
        _seletor.Validar(dataReferencia, hoje, pacote => ValidarNoPacote(documento, pacote.Id));

    private static string? Caminho(XObject? objeto)
    {
        var elemento = objeto switch
        {
            XElement e => e,
            XAttribute a => a.Parent,
            _ => objeto?.Parent,
        };
        if (elemento is null)
        {
            return null;
        }

        var nomes = elemento.AncestorsAndSelf().Reverse().Select(e => e.Name.LocalName);
        var caminho = "/" + string.Join('/', nomes);
        return objeto is XAttribute atributo ? $"{caminho}/@{atributo.Name.LocalName}" : caminho;
    }
}

/// <summary>Violação de schema com localização e pacote (FR-011).</summary>
public sealed record ErroXsd(string Mensagem, int? Linha, int? Posicao, string? Caminho, string Pacote);
