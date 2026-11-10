namespace AuditoriaNFe.Domain.Documentos;

/// <summary>Por que o documento não segue para o schema (FR-007, FR-007a, FR-008).</summary>
public enum MotivoNaoSuportado
{
    /// <summary>Raiz, namespace ou chave de acesso que não são de NF-e (ERR-009).</summary>
    NaoENFe,

    /// <summary><c>ide/mod</c> ou modelo da chave diferente de 55, inclusive NFC-e (ERR-009).</summary>
    ModeloNaoSuportado,

    /// <summary>Atributo <c>versao</c> diferente de 4.00 (ERR-010).</summary>
    VersaoNaoSuportada,
}
