using System.Xml;
using System.Xml.Linq;

namespace AuditoriaNFe.Infrastructure.Xml;

/// <summary>
/// Lê um XML recebido sem DTD, sem resolver externo e com limites de tamanho e profundidade (FR-006).
/// O formato é decidido pelo conteúdo, não pela extensão; conteúdo mal formado é ERR-008.
/// </summary>
public sealed class LeitorXmlSeguro
{
    private readonly int _profundidadeMaxima;
    private readonly long _maxCaracteres;

    public LeitorXmlSeguro(int profundidadeMaxima, long maxCaracteres)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(profundidadeMaxima, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCaracteres, 1);
        _profundidadeMaxima = profundidadeMaxima;
        _maxCaracteres = maxCaracteres;
    }

    /// <summary>Documento com informação de linha, ou <c>null</c> quando o conteúdo não é XML bem formado ou excede os limites.</summary>
    public XDocument? Ler(byte[] conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        try
        {
            using (var verificacao = Criar(conteudo))
            {
                while (verificacao.Read())
                {
                    if (verificacao.Depth >= _profundidadeMaxima)
                    {
                        return null;
                    }
                }
            }

            using var leitor = Criar(conteudo);
            return XDocument.Load(leitor, LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private XmlReader Criar(byte[] conteudo) => XmlReader.Create(
        new MemoryStream(conteudo, writable: false),
        new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = _maxCaracteres,
            MaxCharactersFromEntities = 0,
            IgnoreProcessingInstructions = true,
            IgnoreComments = true,
            CloseInput = true,
        });
}
