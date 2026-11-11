using System.Text.RegularExpressions;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Identificacao;

namespace AuditoriaNFe.Domain.Sessoes;

/// <summary>Contexto conversacional de um usuário (data-model §1; FR-036 a FR-042).</summary>
public sealed partial class Sessao
{
    private List<Guid> _documentos = [];
    private List<Guid> _execucoes = [];
    private Dictionary<string, string> _hashesPorChave = new(StringComparer.Ordinal);

    private Sessao()
    {
        ChaveIsolamento = string.Empty;
        UsuarioId = string.Empty;
        ChamadorId = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Toda leitura confere a chave (FR-039).</summary>
    public string ChaveIsolamento { get; private set; }

    /// <summary>Dado pessoal: nunca em log.</summary>
    public string UsuarioId { get; private set; }

    public string ChamadorId { get; private set; }

    /// <summary>Nome do usuário nas sessões criadas pelo A2A (FR-052d); nunca em log.</summary>
    public string? UsuarioNome { get; private set; }

    /// <summary>Estado gravado. O estado efetivo depende do instante: use <see cref="EstadoEm"/>.</summary>
    public EstadoSessao Estado { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset UltimaAtividadeEm { get; private set; }
    public TimeSpan ExpiraApos { get; private set; }
    public DateTimeOffset? EncerradaEm { get; private set; }

    /// <summary><c>AgentSession</c> serializada do agente interno, isolada pela chave.</summary>
    public string? HistoricoAgente { get; private set; }

    public IReadOnlyList<Guid> Documentos => _documentos;

    /// <summary>Ordem cronológica.</summary>
    public IReadOnlyList<Guid> Execucoes => _execucoes;

    /// <summary>Chave de acesso → SHA-256 do conteúdo (FR-009, FR-037a).</summary>
    public IReadOnlyDictionary<string, string> HashesPorChave => _hashesPorChave;

    public static Sessao Criar(
        Guid id,
        IdentificacaoSolicitante identificacao,
        TimeSpan expiraApos,
        DateTimeOffset agora,
        string? usuarioNome = null)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A sessão exige id gerado pelo sistema.", nameof(id));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiraApos, TimeSpan.Zero);

        return new Sessao
        {
            Id = id,
            ChaveIsolamento = identificacao.ChaveIsolamento,
            UsuarioId = identificacao.UsuarioId,
            ChamadorId = identificacao.ChamadorId,
            UsuarioNome = usuarioNome ?? identificacao.UsuarioNome,
            Estado = EstadoSessao.Ativa,
            CriadaEm = agora,
            UltimaAtividadeEm = agora,
            ExpiraApos = expiraApos,
        };
    }

    public bool PertenceA(string chaveIsolamento) =>
        string.Equals(ChaveIsolamento, chaveIsolamento, StringComparison.Ordinal);

    /// <summary><c>Expirada</c> quando <c>agora - ultimaAtividadeEm &gt; expiraApos</c>.</summary>
    public EstadoSessao EstadoEm(DateTimeOffset agora) =>
        Estado switch
        {
            EstadoSessao.Ativa when agora - UltimaAtividadeEm > ExpiraApos => EstadoSessao.Expirada,
            _ => Estado,
        };

    /// <summary>Grava a expiração quando o prazo de inatividade passou; devolve <c>true</c> na transição.</summary>
    public bool AtualizarExpiracao(DateTimeOffset agora)
    {
        if (Estado != EstadoSessao.Ativa || EstadoEm(agora) != EstadoSessao.Expirada)
        {
            return false;
        }

        Estado = EstadoSessao.Expirada;
        return true;
    }

    /// <summary>Solicitação aceita: renova a atividade. Sessão expirada ou encerrada recusa (ERR-006).</summary>
    public void RegistrarAtividade(DateTimeOffset agora)
    {
        ExigirAtiva(agora);
        if (agora > UltimaAtividadeEm)
        {
            UltimaAtividadeEm = agora;
        }
    }

    /// <summary>Encerramento pelo usuário (FR-041).</summary>
    public void Encerrar(DateTimeOffset agora)
    {
        ExigirAtiva(agora);
        Estado = EstadoSessao.Encerrada;
        EncerradaEm = agora;
    }

    public void RegistrarExecucao(Guid execucaoId, DateTimeOffset agora)
    {
        ExigirAtiva(agora);
        if (execucaoId == Guid.Empty)
        {
            throw new ArgumentException("Execução inválida.", nameof(execucaoId));
        }

        if (!_execucoes.Contains(execucaoId))
        {
            _execucoes.Add(execucaoId);
        }
    }

    public SituacaoDocumentoSessao ClassificarDocumento(string chaveAcesso, string hashConteudo)
    {
        ValidarChaveEHash(chaveAcesso, hashConteudo);
        if (!_hashesPorChave.TryGetValue(chaveAcesso, out var hashExistente))
        {
            return SituacaoDocumentoSessao.Novo;
        }

        return string.Equals(hashExistente, hashConteudo, StringComparison.OrdinalIgnoreCase)
            ? SituacaoDocumentoSessao.Duplicado
            : SituacaoDocumentoSessao.Divergente;
    }

    /// <summary>Acrescenta à base da sessão um documento com chave ainda não vista.</summary>
    public void RegistrarDocumento(Guid documentoId, string chaveAcesso, string hashConteudo, DateTimeOffset agora)
    {
        ExigirAtiva(agora);
        if (documentoId == Guid.Empty)
        {
            throw new ArgumentException("Documento inválido.", nameof(documentoId));
        }

        if (ClassificarDocumento(chaveAcesso, hashConteudo) != SituacaoDocumentoSessao.Novo)
        {
            throw new InvalidOperationException("A chave de acesso já está na base da sessão.");
        }

        _hashesPorChave[chaveAcesso] = hashConteudo.ToLowerInvariant();
        _documentos.Add(documentoId);
    }

    public void AtualizarHistoricoAgente(string? historicoSerializado, DateTimeOffset agora)
    {
        ExigirAtiva(agora);
        HistoricoAgente = historicoSerializado;
    }

    private void ExigirAtiva(DateTimeOffset agora)
    {
        var estado = EstadoEm(agora);
        if (estado != EstadoSessao.Ativa)
        {
            throw new InvalidOperationException($"Sessão {estado}: não aceita novas solicitações.");
        }
    }

    private static void ValidarChaveEHash(string chaveAcesso, string hashConteudo)
    {
        if (!ChaveAcessoNFe.FormatoValido(chaveAcesso))
        {
            throw new ArgumentException("Chave de acesso fora do formato TChNFe.", nameof(chaveAcesso));
        }

        if (hashConteudo is null || !Sha256Hex().IsMatch(hashConteudo))
        {
            throw new ArgumentException("Hash deve ser SHA-256 hexadecimal.", nameof(hashConteudo));
        }
    }

    [GeneratedRegex(@"\A[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Hex();
}
