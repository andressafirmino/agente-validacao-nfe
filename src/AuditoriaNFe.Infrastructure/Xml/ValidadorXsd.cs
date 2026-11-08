using System.Xml.Linq;
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
    public ValidadorXsd(ProvedorPacotesSchema provedor) => throw new NotImplementedException("T073");

    public IReadOnlyList<ErroXsd> ValidarNoPacote(XDocument documento, string pacoteId) =>
        throw new NotImplementedException("T073");

    public ResultadoPacote<ErroXsd> Validar(XDocument documento, DateOnly dataReferencia, DateOnly hoje) =>
        throw new NotImplementedException("T073");
}

/// <summary>Violação de schema com localização e pacote (FR-011).</summary>
public sealed record ErroXsd(string Mensagem, int? Linha, int? Posicao, string? Caminho, string Pacote);
