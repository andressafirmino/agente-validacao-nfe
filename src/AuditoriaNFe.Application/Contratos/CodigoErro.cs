namespace AuditoriaNFe.Application.Contratos;

/// <summary>Códigos estáveis de erro (spec, Error Handling).</summary>
public static class CodigoErro
{
    public const string SolicitacaoSemConteudo = "ERR-001";
    public const string TamanhoExcedido = "ERR-002";
    public const string QuantidadeArquivosExcedida = "ERR-003";

    /// <summary>Reservado e sem uso enquanto vigorar o desvio D-05.</summary>
    public const string NaoAutorizado = "ERR-004";

    public const string SessaoNaoEncontrada = "ERR-005";
    public const string SessaoExpirada = "ERR-006";
    public const string MensagemForaDoContrato = "ERR-007";
    public const string XmlMalformado = "ERR-008";
    public const string DocumentoNaoSuportado = "ERR-009";
    public const string VersaoNaoSuportada = "ERR-010";
    public const string DocumentoDuplicado = "ERR-011";
    public const string ConteudoDivergente = "ERR-012";
    public const string BaseConhecimentoIndisponivel = "ERR-013";
    public const string ProvedorIaIndisponivel = "ERR-014";
    public const string RespostaIaInvalida = "ERR-015";
    public const string TempoMaximoEsgotado = "ERR-016";
    public const string InformacaoIndisponivel = "ERR-017";
    public const string ForaDoEscopo = "ERR-018";

    /// <summary>Reservado e sem uso enquanto vigorar o desvio D-05.</summary>
    public const string SemPapelAprovador = "ERR-019";

    public const string RecomendacaoJaDecidida = "ERR-020";
    public const string JustificativaObrigatoria = "ERR-021";
    public const string FalhaCheckpoint = "ERR-022";
    public const string SchemaIndisponivelParaData = "ERR-023";
    public const string LimiteAtingido = "ERR-024";
    public const string ArmazenamentoIndisponivel = "ERR-025";
    public const string SolicitacaoEmAndamento = "ERR-026";
    public const string RequisicaoCancelada = "ERR-027";

    /// <summary>Códigos sem uso enquanto vigorar o desvio D-05.</summary>
    public static readonly IReadOnlySet<string> Reservados = new HashSet<string>([NaoAutorizado, SemPapelAprovador]);

    public static readonly IReadOnlyList<string> Todos =
    [
        SolicitacaoSemConteudo, TamanhoExcedido, QuantidadeArquivosExcedida, NaoAutorizado, SessaoNaoEncontrada,
        SessaoExpirada, MensagemForaDoContrato, XmlMalformado, DocumentoNaoSuportado, VersaoNaoSuportada,
        DocumentoDuplicado, ConteudoDivergente, BaseConhecimentoIndisponivel, ProvedorIaIndisponivel,
        RespostaIaInvalida, TempoMaximoEsgotado, InformacaoIndisponivel, ForaDoEscopo, SemPapelAprovador,
        RecomendacaoJaDecidida, JustificativaObrigatoria, FalhaCheckpoint, SchemaIndisponivelParaData,
        LimiteAtingido, ArmazenamentoIndisponivel, SolicitacaoEmAndamento, RequisicaoCancelada,
    ];
}
