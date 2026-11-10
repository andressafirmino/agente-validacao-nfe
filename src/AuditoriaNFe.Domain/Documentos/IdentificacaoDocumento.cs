using System.Globalization;
using System.Text.RegularExpressions;

namespace AuditoriaNFe.Domain.Documentos;

/// <summary>
/// Resultado da identificação de um XML recebido (data-model §3, FR-007, FR-007a, FR-008, FR-010a).
/// Os campos vêm do XML como texto; só documentos suportados seguem para o schema.
/// </summary>
public sealed partial record IdentificacaoDocumento
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
        string? dhRecbto)
    {
        ArgumentNullException.ThrowIfNull(elementoRaiz);

        var chave = idInfNFe is not null && idInfNFe.StartsWith(PrefixoId, StringComparison.Ordinal)
            ? idInfNFe[PrefixoId.Length..]
            : null;
        var chaveValida = ChaveAcessoNFe.FormatoValido(chave);
        var modeloChave = chaveValida ? chave![20..22] : null;

        MotivoNaoSuportado? motivo =
            elementoRaiz is not ("NFe" or "nfeProc") || !string.Equals(@namespace, NamespaceNFe, StringComparison.Ordinal) || !chaveValida
                ? Documentos.MotivoNaoSuportado.NaoENFe
                : modeloIde != ModeloSuportado || modeloChave != ModeloSuportado
                    ? Documentos.MotivoNaoSuportado.ModeloNaoSuportado
                    : versaoLayout != VersaoSuportada
                        ? Documentos.MotivoNaoSuportado.VersaoNaoSuportada
                        : null;

        DateOnly? dataReferencia = null;
        OrigemDataReferencia? origem = null;
        if (motivo is null)
        {
            if (possuiProtocolo && DataDeCalendario(dhRecbto) is { } dataRecbto)
            {
                dataReferencia = dataRecbto;
                origem = Documentos.OrigemDataReferencia.DhRecbto;
            }
            else if (DataDeCalendario(dhEmi) is { } dataEmi)
            {
                dataReferencia = dataEmi;
                origem = Documentos.OrigemDataReferencia.DhEmi;
            }
        }

        return new IdentificacaoDocumento
        {
            ElementoRaiz = elementoRaiz,
            Namespace = @namespace,
            VersaoLayout = versaoLayout,
            ModeloIde = modeloIde,
            ModeloChave = modeloChave,
            ChaveAcesso = chaveValida ? chave : null,
            PossuiProtocolo = possuiProtocolo,
            DhEmi = dhEmi,
            DhRecbto = dhRecbto,
            MotivoNaoSuportado = motivo,
            DataReferencia = dataReferencia,
            OrigemDataReferencia = origem,
        };
    }

    /// <summary>
    /// O que impediu o suporte, para o motivo de ERR-009 e ERR-010 (FR-008). Repete do XML só o
    /// nome da raiz, o modelo e a versão quando bem formados; nunca o namespace nem a chave recebidos.
    /// </summary>
    public string? DescreverMotivo() => MotivoNaoSuportado switch
    {
        null => null,
        Documentos.MotivoNaoSuportado.NaoENFe when ElementoRaiz is not ("NFe" or "nfeProc") => DescreverRaiz(ElementoRaiz),
        Documentos.MotivoNaoSuportado.NaoENFe when !string.Equals(Namespace, NamespaceNFe, StringComparison.Ordinal) =>
            $"o XML não usa o namespace da NF-e (`{NamespaceNFe}`)",
        Documentos.MotivoNaoSuportado.NaoENFe => "a chave de acesso (atributo `Id` de `infNFe`) está ausente ou inválida",
        Documentos.MotivoNaoSuportado.ModeloNaoSuportado when ModeloIde == "65" && ModeloChave == "65" => "o documento é uma NFC-e (modelo 65)",
        Documentos.MotivoNaoSuportado.ModeloNaoSuportado when ModeloIde != ModeloChave =>
            $"o modelo em `ide/mod` ({Modelo(ModeloIde)}) é diferente do modelo na chave de acesso ({Modelo(ModeloChave)})",
        Documentos.MotivoNaoSuportado.ModeloNaoSuportado => $"o modelo do documento é {Modelo(ModeloIde)}",
        _ => VersaoLayout switch
        {
            null or "" => "versão de layout não informada",
            var versao when FormatoVersao().IsMatch(versao) => $"versão de layout {versao}",
            _ => "versão de layout inválida",
        },
    };

    private static string DescreverRaiz(string raiz)
    {
        if (!FormatoNome().IsMatch(raiz))
        {
            return "o elemento raiz não é `NFe` nem `nfeProc`";
        }

        var tipo = raiz switch
        {
            "procEventoNFe" or "evento" or "envEvento" or "retEnvEvento" => "um evento de NF-e",
            "resEvento" => "um resumo de evento de NF-e",
            "resNFe" => "um resumo de NF-e",
            "enviNFe" => "um lote de envio de NF-e à SEFAZ",
            "procInutNFe" or "inutNFe" => "uma inutilização de numeração de NF-e",
            "cteProc" or "CTe" or "cteOSProc" or "CTeOS" => "um CT-e",
            "nfseProc" or "NFSe" or "DPS" => "uma NFS-e",
            _ => null,
        };
        return tipo is null
            ? $"o elemento raiz é `{raiz}`; uma NF-e tem raiz `NFe` ou `nfeProc`"
            : $"o arquivo é {tipo} (raiz `{raiz}`), não uma NF-e";
    }

    private static string Modelo(string? modelo) =>
        modelo is { Length: 2 } && char.IsAsciiDigit(modelo[0]) && char.IsAsciiDigit(modelo[1]) ? modelo : "inválido";

    [GeneratedRegex(@"^\d{1,2}\.\d{2}$")]
    private static partial Regex FormatoVersao();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.\-]{0,39}$")]
    private static partial Regex FormatoNome();

    private const string PrefixoId = "NFe";

    /// <summary>A data escrita no XML (<c>AAAA-MM-DD</c> antes do <c>T</c>), sem conversão de fuso.</summary>
    private static DateOnly? DataDeCalendario(string? dataHora) =>
        dataHora is { Length: >= 10 }
        && DateOnly.TryParseExact(dataHora.AsSpan(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;
}
