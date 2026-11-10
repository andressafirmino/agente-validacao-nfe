using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Schemas;

namespace AuditoriaNFe.Application.Abstracoes;

/// <summary>
/// Leitura segura, identificação, validação de layout e extração de um XML recebido (etapas 2 a 4).
/// O documento lido só vive durante a execução e nunca é persistido (FR-037a).
/// </summary>
public interface IProcessadorXml
{
    /// <summary>Versão do manifesto de schemas carregado.</summary>
    string VersaoManifesto { get; }

    /// <summary>Nulo quando o conteúdo não é XML bem formado (ERR-008).</summary>
    DocumentoXmlLido? Ler(byte[] conteudo);

    IdentificacaoDocumento Identificar(DocumentoXmlLido documento);

    ResultadoPacote<ErroLayout> ValidarLayout(DocumentoXmlLido documento, DateOnly dataReferencia, DateOnly hoje);

    /// <summary>Só os campos do catálogo de extração, cada um com o caminho no XML.</summary>
    IReadOnlyList<CampoExtraido> Extrair(DocumentoXmlLido documento);
}

/// <summary>XML bem formado, opaco para a Aplicação.</summary>
public abstract class DocumentoXmlLido;

/// <summary>Violação de schema com localização e pacote (FR-011).</summary>
public sealed record ErroLayout(string Mensagem, int? Linha, int? Posicao, string? Caminho, string Pacote);
