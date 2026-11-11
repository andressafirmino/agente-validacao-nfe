using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Identificacao;
using AuditoriaNFe.Domain.Parecer;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Domain.Sessoes;

/// <summary>
/// Processamento de uma solicitação (data-model §2; FR-054, NFR-008). As nove etapas da
/// constitution 3.4 estão sempre presentes; nenhuma é omitida.
/// </summary>
public sealed class Execucao
{
    public const int TamanhoMaximoCorrelacao = 128;

    /// <summary>Erros que rejeitam a execução antes do processamento (diagrama de estados).</summary>
    public static readonly IReadOnlySet<string> CodigosRejeicao =
        new HashSet<string>(["ERR-001", "ERR-002", "ERR-003", "ERR-005", "ERR-006", "ERR-007"], StringComparer.Ordinal);

    /// <summary>Dependências indisponíveis com que a execução ainda conclui, com revisão humana (ERR-013 a ERR-015).</summary>
    public static readonly IReadOnlySet<string> CodigosDependencia =
        new HashSet<string>(["ERR-013", "ERR-014", "ERR-015"], StringComparer.Ordinal);

    private List<EtapaExecucao> _etapas = [];
    private List<ArquivoRecebido> _arquivos = [];
    private List<ChamadaFerramenta> _chamadasFerramentas = [];
    private List<TrechoConsultado> _trechosConsultados = [];
    private Dictionary<string, string> _versoes = new(StringComparer.Ordinal);

    private Execucao()
    {
        ChaveIsolamento = string.Empty;
        UsuarioId = string.Empty;
        ChamadorId = string.Empty;
    }

    public Guid Id { get; private set; }
    public string? CorrelacaoId { get; private set; }
    public Guid? IdExecucaoSofia { get; private set; }
    public Guid? IdSolicitacaoSofia { get; private set; }
    public string? ChaveIdempotenciaSofia { get; private set; }
    public Guid? SessaoId { get; private set; }
    public string ChaveIsolamento { get; private set; }
    public string UsuarioId { get; private set; }
    public string ChamadorId { get; private set; }

    /// <summary>Só no A2A; nunca em log (data-model §2).</summary>
    public string? UsuarioNome { get; private set; }

    public bool UsuarioIdentificado { get; private set; }
    public CanalExecucao Canal { get; private set; }
    public TipoExecucao Tipo { get; private set; }

    /// <summary>Pode conter dados pessoais: nunca em log.</summary>
    public string? Pergunta { get; private set; }

    public EstadoExecucao Estado { get; private set; }
    public ErroExecucao? Erro { get; private set; }
    public DateTimeOffset IniciadaEm { get; private set; }
    public DateTimeOffset? ConcluidaEm { get; private set; }
    public bool IntegracaoMcpHabilitada { get; private set; }
    public string? Modelo { get; private set; }
    public ConsumoResumo? ConsumoResumo { get; private set; }
    public bool ContextoUsuarioAtualizado { get; private set; }

    public IReadOnlyList<EtapaExecucao> Etapas => _etapas;

    /// <summary>Arquivos recebidos, na ordem de envio, sem o conteúdo (data-model §3).</summary>
    public IReadOnlyList<ArquivoRecebido> Arquivos => _arquivos;

    public ParecerExecucao? Parecer { get; private set; }

    public IReadOnlyList<ChamadaFerramenta> ChamadasFerramentas => _chamadasFerramentas;
    public IReadOnlyList<TrechoConsultado> TrechosConsultados => _trechosConsultados;

    /// <summary>Versões do envelope, contrato do LLM, manifesto, catálogos, instruções e tabela de preços.</summary>
    public IReadOnlyDictionary<string, string> Versoes => _versoes;

    public bool Finalizada => Estado is EstadoExecucao.Concluida or EstadoExecucao.Falhou or EstadoExecucao.Rejeitada;

    public static Execucao Receber(
        Guid id,
        IdentificacaoSolicitante identificacao,
        CanalExecucao canal,
        TipoExecucao tipo,
        DateTimeOffset agora,
        string? correlacaoId = null,
        string? pergunta = null,
        bool integracaoMcpHabilitada = false,
        IReadOnlyDictionary<string, string>? versoes = null)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A execução exige id gerado pelo sistema.", nameof(id));
        }

        if (correlacaoId is { Length: > TamanhoMaximoCorrelacao })
        {
            throw new ArgumentException($"correlacaoId aceita no máximo {TamanhoMaximoCorrelacao} caracteres.", nameof(correlacaoId));
        }

        return new Execucao
        {
            Id = id,
            CorrelacaoId = correlacaoId,
            ChaveIsolamento = identificacao.ChaveIsolamento,
            UsuarioId = identificacao.UsuarioId,
            ChamadorId = identificacao.ChamadorId,
            UsuarioIdentificado = identificacao.UsuarioIdentificado,
            Canal = canal,
            Tipo = tipo,
            Pergunta = pergunta,
            Estado = EstadoExecucao.Recebida,
            IniciadaEm = agora,
            IntegracaoMcpHabilitada = integracaoMcpHabilitada,
            _etapas = EtapaExecucao.CriarTodas(),
            _versoes = versoes is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(versoes, StringComparer.Ordinal),
        };
    }

    public void AssociarSessao(Guid sessaoId)
    {
        ExigirNaoFinalizada();
        if (sessaoId == Guid.Empty)
        {
            throw new ArgumentException("Sessão inválida.", nameof(sessaoId));
        }

        if (SessaoId is not null && SessaoId != sessaoId)
        {
            throw new InvalidOperationException("A execução já está associada a outra sessão.");
        }

        SessaoId = sessaoId;
    }

    public void ConfigurarSofia(Guid idExecucao, Guid idSolicitacao, string? usuarioNome, string chaveIdempotencia)
    {
        ExigirNaoFinalizada();
        if (idExecucao == Guid.Empty || idSolicitacao == Guid.Empty)
        {
            throw new ArgumentException("Ids da SOFIA inválidos.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(chaveIdempotencia);
        IdExecucaoSofia = idExecucao;
        IdSolicitacaoSofia = idSolicitacao;
        UsuarioNome = usuarioNome;
        ChaveIdempotenciaSofia = chaveIdempotencia;
    }

    public static string MontarChaveIdempotencia(string chamadorId, Guid idSolicitacao) =>
        $"{chamadorId}{Identificacao.IdentificacaoSolicitante.Separador}{idSolicitacao:D}";

    public void IniciarProcessamento()
    {
        if (Estado != EstadoExecucao.Recebida)
        {
            throw TransicaoProibida(EstadoExecucao.EmProcessamento);
        }

        Estado = EstadoExecucao.EmProcessamento;
    }

    public void RegistrarEtapa(
        int numero,
        EstadoEtapa estado,
        DateTimeOffset? inicioEm = null,
        DateTimeOffset? fimEm = null,
        string? justificativa = null)
    {
        ExigirNaoFinalizada();
        Etapa(numero).Registrar(estado, inicioEm, fimEm, justificativa);
    }

    public void RegistrarArquivo(ArquivoRecebido arquivo)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ExigirNaoFinalizada();
        _arquivos.Add(arquivo);
    }

    public void DefinirParecer(ParecerExecucao parecer)
    {
        ArgumentNullException.ThrowIfNull(parecer);
        ExigirNaoFinalizada();
        Parecer = parecer;
    }

    /// <param name="erroDependencia">ERR-013 a ERR-015: a execução conclui com o erro registrado.</param>
    public void Concluir(DateTimeOffset agora, ErroExecucao? erroDependencia = null)
    {
        if (Estado != EstadoExecucao.EmProcessamento)
        {
            throw TransicaoProibida(EstadoExecucao.Concluida);
        }

        if (erroDependencia is not null && !CodigosDependencia.Contains(erroDependencia.Codigo))
        {
            throw new ArgumentException($"{erroDependencia.Codigo} não conclui uma execução.", nameof(erroDependencia));
        }

        var pendentes = _etapas.Where(e => !e.Registrada).Select(e => e.Numero).ToList();
        if (pendentes.Count > 0)
        {
            throw new InvalidOperationException(
                $"Etapas sem estado registrado: {string.Join(", ", pendentes)} (constitution 3.4).");
        }

        Estado = EstadoExecucao.Concluida;
        Erro = erroDependencia;
        ConcluidaEm = agora;
    }

    /// <summary>Tempo esgotado (ERR-016) ou falha não recuperável durante o processamento.</summary>
    public void Falhar(ErroExecucao erro, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(erro);
        if (Estado != EstadoExecucao.EmProcessamento)
        {
            throw TransicaoProibida(EstadoExecucao.Falhou);
        }

        Encerrar(EstadoExecucao.Falhou, erro, agora);
    }

    /// <summary>Rejeição antes do processamento: ERR-001 a ERR-003 e ERR-005 a ERR-007.</summary>
    public void Rejeitar(ErroExecucao erro, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(erro);
        if (Estado != EstadoExecucao.Recebida)
        {
            throw TransicaoProibida(EstadoExecucao.Rejeitada);
        }

        if (!CodigosRejeicao.Contains(erro.Codigo))
        {
            throw new ArgumentException($"{erro.Codigo} não rejeita uma execução.", nameof(erro));
        }

        Encerrar(EstadoExecucao.Rejeitada, erro, agora);
    }

    public void DefinirModelo(string modelo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelo);
        ExigirNaoFinalizada();
        Modelo = modelo;
    }

    public void DefinirConsumo(ConsumoResumo consumo)
    {
        ArgumentNullException.ThrowIfNull(consumo);
        ConsumoResumo = consumo;
    }

    public void MarcarContextoUsuarioAtualizado()
    {
        ExigirNaoFinalizada();
        ContextoUsuarioAtualizado = true;
    }

    public void RegistrarChamadaFerramenta(ChamadaFerramenta chamada)
    {
        ArgumentNullException.ThrowIfNull(chamada);
        ExigirNaoFinalizada();
        _chamadasFerramentas.Add(chamada);
    }

    public void RegistrarTrechoConsultado(TrechoConsultado trecho)
    {
        ArgumentNullException.ThrowIfNull(trecho);
        ExigirNaoFinalizada();
        _trechosConsultados.Add(trecho);
    }

    public EtapaExecucao Etapa(int numero) =>
        numero is >= 1 and <= EtapaExecucao.Quantidade
            ? _etapas[numero - 1]
            : throw new ArgumentOutOfRangeException(nameof(numero), numero, "A etapa vai de 1 a 9.");

    private void Encerrar(EstadoExecucao estado, ErroExecucao erro, DateTimeOffset agora)
    {
        var justificativa = $"Não executada: a execução terminou com {erro.Codigo}.";
        foreach (var etapa in _etapas.Where(e => !e.Registrada))
        {
            etapa.Registrar(EstadoEtapa.NaoExecutada, null, null, justificativa);
        }

        Estado = estado;
        Erro = erro;
        ConcluidaEm = agora;
        if (estado == EstadoExecucao.Falhou)
        {
            ChaveIdempotenciaSofia = null;
        }
    }

    private void ExigirNaoFinalizada()
    {
        if (Finalizada)
        {
            throw new InvalidOperationException($"A execução já terminou ({Estado}).");
        }
    }

    private InvalidOperationException TransicaoProibida(EstadoExecucao destino) =>
        new($"Transição proibida: {Estado} → {destino}.");
}

/// <summary>Estado de uma das nove etapas (constitution 3.4; data-model §2).</summary>
public sealed class EtapaExecucao
{
    public const int Quantidade = 9;

    /// <summary>Nomes das etapas, na ordem da constitution 3.4.</summary>
    public static readonly IReadOnlyList<string> Nomes =
    [
        "Receber e identificar",
        "Validar tamanho, formato e condições",
        "Identificar tipo e versão do layout",
        "Validar estrutura e schema",
        "Consultar manuais por MCP",
        "Executar ferramentas externas",
        "Analisar com o LLM",
        "Validar a saída estruturada",
        "Retornar o resultado",
    ];

    private EtapaExecucao()
    {
        Nome = string.Empty;
    }

    public int Numero { get; private set; }
    public string Nome { get; private set; }
    public EstadoEtapa Estado { get; private set; }

    /// <summary>Obrigatória para <c>NaoAplicavel</c> e <c>NaoExecutada</c>.</summary>
    public string? Justificativa { get; private set; }

    public DateTimeOffset? InicioEm { get; private set; }
    public DateTimeOffset? FimEm { get; private set; }

    /// <summary><c>false</c> enquanto o fluxo não registrou o estado da etapa.</summary>
    public bool Registrada { get; private set; }

    internal static List<EtapaExecucao> CriarTodas() =>
        Enumerable.Range(1, Quantidade)
            .Select(n => new EtapaExecucao
            {
                Numero = n,
                Nome = Nomes[n - 1],
                Estado = EstadoEtapa.NaoExecutada,
                Justificativa = "Etapa ainda não alcançada.",
            })
            .ToList();

    internal void Registrar(EstadoEtapa estado, DateTimeOffset? inicioEm, DateTimeOffset? fimEm, string? justificativa)
    {
        if ((estado is EstadoEtapa.NaoAplicavel or EstadoEtapa.NaoExecutada) && string.IsNullOrWhiteSpace(justificativa))
        {
            throw new ArgumentException($"A etapa {Numero} em {estado} exige justificativa.", nameof(justificativa));
        }

        if (inicioEm is not null && fimEm is not null && fimEm < inicioEm)
        {
            throw new ArgumentException("O fim da etapa não pode ser anterior ao início.", nameof(fimEm));
        }

        Estado = estado;
        InicioEm = inicioEm;
        FimEm = fimEm;
        Justificativa = justificativa;
        Registrada = true;
    }
}
