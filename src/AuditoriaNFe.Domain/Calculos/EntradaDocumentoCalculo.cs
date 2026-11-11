using AuditoriaNFe.Domain.Documentos;

namespace AuditoriaNFe.Domain.Calculos;

/// <summary>Documento da base da sessão elegível para soma de campo catalogado.</summary>
public sealed record EntradaDocumentoCalculo(
    string ChaveAcesso,
    IReadOnlyList<CampoExtraido> Campos,
    MotivoExclusaoCalculo? Situacao = null);
