namespace AuditoriaNFe.Domain.Documentos;

/// <summary>
/// Resultado da identificação de um XML recebido (data-model §3, FR-007, FR-007a, FR-008, FR-010a).
/// Os campos vêm do XML como texto; só documentos suportados seguem para o schema.
/// </summary>
public sealed record IdentificacaoDocumento
{
    public const string NamespaceNFe = "http://www.portalfiscal.inf.br/nfe";
    public const string ModeloSuportado = "55";
    public const string VersaoSuportada = "4.00";

    public required string ElementoRaiz { get; init; }
    public string? Namespace { get; init; }
    public string? VersaoLayout { get; init; }
    public string? ModeloIde { get; init; }

    /// <summary>Posições 21–22 da chave de acesso.</summary>
    public string? ModeloChave { get; init; }

    /// <summary>44 caracteres, do atributo <c>Id</c> de <c>infNFe</c> sem o prefixo <c>NFe</c>.</summary>
    public string? ChaveAcesso { get; init; }

    public bool PossuiProtocolo { get; init; }

    /// <summary>Como no XML, com o deslocamento de fuso.</summary>
    public string? DhEmi { get; init; }

    /// <summary>Como no XML, quando houver protocolo.</summary>
    public string? DhRecbto { get; init; }

    public bool Suportado => MotivoNaoSuportado is null;
    public MotivoNaoSuportado? MotivoNaoSuportado { get; init; }

    /// <summary>Data de calendário de <c>dhRecbto</c> (com protocolo) ou de <c>dhEmi</c>, no deslocamento do XML.</summary>
    public DateOnly? DataReferencia { get; init; }

    public OrigemDataReferencia? OrigemDataReferencia { get; init; }

    /// <summary>Monta a identificação a partir dos valores lidos do XML, na ordem raiz, namespace, chave, modelo e versão.</summary>
    public static IdentificacaoDocumento Avaliar(
        string elementoRaiz,
        string? @namespace,
        string? versaoLayout,
        string? modeloIde,
        string? idInfNFe,
        bool possuiProtocolo,
        string? dhEmi,
        string? dhRecbto) =>
        throw new NotImplementedException("T061");
}
