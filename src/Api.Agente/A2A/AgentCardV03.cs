namespace Api.Agente.A2A;

// Agent Card no perfil A2A v0.3 do contrato SOFIA v1.0 (contracts/sofia-a2a.md §2). A ordem das
// propriedades é a do exemplo do contrato, conferida pelo teste de contrato.

public sealed record AgentCardV03(
    string Name,
    string Description,
    string Url,
    string Version,
    string ProtocolVersion,
    string PreferredTransport,
    CapacidadesAgentCard Capabilities,
    IReadOnlyList<string> DefaultInputModes,
    IReadOnlyList<string> DefaultOutputModes,
    IReadOnlyList<HabilidadeAgentCard> Skills,
    IReadOnlyDictionary<string, EsquemaSegurancaAgentCard> SecuritySchemes,
    IReadOnlyList<IReadOnlyDictionary<string, IReadOnlyList<string>>> Security);

/// <summary>Esquema declarado no card, sem o valor da credencial (FR-052h).</summary>
public sealed record EsquemaSegurancaAgentCard(string Type, string Scheme);

public sealed record CapacidadesAgentCard(
    bool Streaming,
    bool PushNotifications,
    IReadOnlyList<ExtensaoAgentCard> Extensions);

public sealed record ExtensaoAgentCard(
    string Uri,
    string Description,
    bool Required,
    ParametrosContratoSofia Params);

/// <summary><c>Implantacao</c> só aparece nos modos <c>SomenteAgentCard</c> e <c>Homologacao</c> (FR-052f, FR-052g).</summary>
public sealed record ParametrosContratoSofia(
    string VersaoContrato,
    string IdAgente,
    string NomeArtifact,
    string Autenticacao,
    LimitesContratoSofia Limites,
    IReadOnlyList<DocumentoSuportadoSofia> DocumentosSuportados,
    string Idioma,
    string? Implantacao);

public sealed record LimitesContratoSofia(long TamanhoTotalBytes, int QuantidadeDocumentos);

public sealed record DocumentoSuportadoSofia(
    string Tipo,
    string Modelo,
    IReadOnlyList<string> VersoesLayout,
    string TipoMime);

public sealed record HabilidadeAgentCard(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Examples);
