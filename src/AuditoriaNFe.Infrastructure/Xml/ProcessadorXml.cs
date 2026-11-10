using System.Globalization;
using System.Xml.Linq;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Schemas;
using AuditoriaNFe.Infrastructure.Schemas;

namespace AuditoriaNFe.Infrastructure.Xml;

/// <summary>Etapas 2 a 4 sobre o XML: <see cref="LeitorXmlSeguro"/>, <see cref="IdentificadorNFe"/>, <see cref="ValidadorXsd"/> e <see cref="ExtratorDados"/>.</summary>
public sealed class ProcessadorXml(LeitorXmlSeguro leitor, ValidadorXsd validador, ExtratorDados extrator, ProvedorPacotesSchema provedor) : IProcessadorXml
{
    public string VersaoManifesto { get; } = provedor.VersaoManifesto.ToString(CultureInfo.InvariantCulture);

    public DocumentoXmlLido? Ler(byte[] conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        return leitor.Ler(conteudo) is { } documento ? new DocumentoLido(documento) : null;
    }

    public IdentificacaoDocumento Identificar(DocumentoXmlLido documento) => IdentificadorNFe.Identificar(Xml(documento));

    public ResultadoPacote<ErroLayout> ValidarLayout(DocumentoXmlLido documento, DateOnly dataReferencia, DateOnly hoje)
    {
        var resultado = validador.Validar(Xml(documento), dataReferencia, hoje);
        return new ResultadoPacote<ErroLayout>
        {
            DataReferencia = resultado.DataReferencia,
            PacoteVigente = resultado.PacoteVigente,
            PacoteAplicado = resultado.PacoteAplicado,
            PacotesTentados = resultado.PacotesTentados,
            EmConvivencia = resultado.EmConvivencia,
            Erros = [.. resultado.Erros.Select(e => new ErroLayout(e.Mensagem, e.Linha, e.Posicao, e.Caminho, e.Pacote))],
        };
    }

    public IReadOnlyList<CampoExtraido> Extrair(DocumentoXmlLido documento) => extrator.Extrair(Xml(documento));

    private static XDocument Xml(DocumentoXmlLido documento) =>
        documento is DocumentoLido lido ? lido.Documento : throw new ArgumentException("Documento de outro processador.", nameof(documento));

    private sealed class DocumentoLido(XDocument documento) : DocumentoXmlLido
    {
        public XDocument Documento { get; } = documento;
    }
}
