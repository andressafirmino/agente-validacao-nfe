using System.Globalization;

namespace AuditoriaNFe.Application.Contratos;

/// <summary>
/// Mensagens em português do Brasil para cada código (spec, Error Handling; NFR-010). Nenhuma traz
/// detalhes internos; os números do erro vão em <c>erro.detalhes</c>.
/// </summary>
public static class MensagensErro
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly Dictionary<string, string> Padrao = new(StringComparer.Ordinal)
    {
        [CodigoErro.SolicitacaoSemConteudo] = "A solicitação não tem pergunta nem arquivos. Envie ao menos uma pergunta ou um XML de NF-e.",
        [CodigoErro.TamanhoExcedido] = "O tamanho total dos arquivos excede o limite permitido. Nenhum arquivo foi processado.",
        [CodigoErro.QuantidadeArquivosExcedida] = "A quantidade de arquivos excede o limite permitido. Nenhum arquivo foi processado.",
        [CodigoErro.NaoAutorizado] = "Acesso não autorizado.",
        [CodigoErro.SessaoNaoEncontrada] = "Sessão não encontrada.",
        [CodigoErro.SessaoExpirada] = "Sessão expirada. Inicie uma nova sessão enviando a solicitação sem o identificador de sessão.",
        [CodigoErro.MensagemForaDoContrato] = "A mensagem está malformada ou fora do contrato. Nenhuma análise foi executada.",
        [CodigoErro.XmlMalformado] = "O arquivo não é um XML bem formado.",
        [CodigoErro.DocumentoNaoSuportado] = "Validação não realizada: documento não suportado. Apenas NF-e modelo 55 é aceita.",
        [CodigoErro.VersaoNaoSuportada] = "Validação não realizada: versão de layout não suportada. Apenas a versão 4.00 é aceita.",
        [CodigoErro.DocumentoDuplicado] = "Documento duplicado no lote ou na sessão; ele foi analisado uma única vez.",
        [CodigoErro.ConteudoDivergente] = "Há documentos com a mesma chave de acesso e conteúdos diferentes.",
        [CodigoErro.BaseConhecimentoIndisponivel] = "A base de manuais oficiais está indisponível. Os resultados determinísticos foram entregues e o parecer exige revisão humana.",
        [CodigoErro.ProvedorIaIndisponivel] = "O provedor de IA está indisponível ou não respondeu a tempo. Os resultados determinísticos foram entregues, sem análise por IA e sem recomendações.",
        [CodigoErro.RespostaIaInvalida] = "A resposta da IA foi inválida em todas as tentativas e foi descartada. Os resultados determinísticos foram entregues, sem análise por IA e sem recomendações.",
        [CodigoErro.TempoMaximoEsgotado] = "O tempo máximo da execução se esgotou. As etapas concluídas e as não executadas foram registradas.",
        [CodigoErro.InformacaoIndisponivel] = "A informação pedida não existe nos documentos analisados nesta sessão.",
        [CodigoErro.ForaDoEscopo] = "A pergunta está fora do escopo do agente, que audita NF-e modelo 55 enviadas nesta sessão.",
        [CodigoErro.SemPapelAprovador] = "Operação não permitida para este usuário.",
        [CodigoErro.RecomendacaoJaDecidida] = "A recomendação não existe ou já foi decidida; a decisão original foi mantida.",
        [CodigoErro.JustificativaObrigatoria] = "A justificativa é obrigatória para rejeitar uma recomendação.",
        [CodigoErro.FalhaCheckpoint] = "Não foi possível registrar a decisão. A recomendação continua pendente de aprovação.",
        [CodigoErro.SchemaIndisponivelParaData] = "Validação de layout não realizada: schema não disponível para a data de referência do documento.",
        [CodigoErro.LimiteAtingido] = "Limite de solicitações atingido. Tente novamente mais tarde.",
        [CodigoErro.ArmazenamentoIndisponivel] = "O armazenamento de sessões está indisponível. Nenhuma análise foi executada; tente novamente mais tarde.",
    };

    public const string SessaoEncerrada = "Sessão encerrada. Inicie uma nova sessão enviando a solicitação sem o identificador de sessão.";

    public const string ContextoUsuarioNaoEncontrado = "Não há contexto do usuário para esta identificação.";

    public const string UsuarioSemContexto = "Usuário não identificado não tem contexto. Informe o cabeçalho X-Usuario-Id.";

    public const string PreferenciasForaDoContrato =
        "Preferências fora do contrato: informe nivelDetalhe (Resumido ou Detalhado) e tributosInteresse, sem repetição, só com ICMS, ICMS-ST, FCP, IPI, PIS, COFINS, ISSQN, IBS, CBS ou IS.";

    public static string Para(string codigo) =>
        Padrao.TryGetValue(codigo, out var mensagem)
            ? mensagem
            : throw new ArgumentOutOfRangeException(nameof(codigo), codigo, "Código de erro desconhecido.");

    public static string TamanhoExcedido(long limiteBytes, long totalRecebidoBytes) =>
        string.Format(
            PtBr,
            "O tamanho total dos arquivos ({0:N0} bytes) excede o limite de {1:N0} bytes. Nenhum arquivo foi processado.",
            totalRecebidoBytes,
            limiteBytes);

    public static string QuantidadeArquivosExcedida(int limite) =>
        string.Format(PtBr, "A quantidade de arquivos excede o limite de {0} por solicitação. Nenhum arquivo foi processado.", limite);

    /// <summary>ERR-009 com o que foi encontrado no arquivo (FR-008).</summary>
    public static string DocumentoNaoSuportado(string motivo) =>
        $"Validação não realizada: documento não suportado — {motivo}. Apenas NF-e modelo 55 é aceita.";

    /// <summary>ERR-010 com a versão encontrada no arquivo (FR-008).</summary>
    public static string VersaoNaoSuportada(string motivo) =>
        $"Validação não realizada: {motivo} não suportada. Apenas a versão 4.00 é aceita.";

    public static string LimiteAtingido(int segundosParaNovaTentativa) =>
        string.Format(PtBr, "Limite de solicitações atingido. Tente novamente em {0} segundo(s).", segundosParaNovaTentativa);
}
