using System.Text;
using System.Text.RegularExpressions;
using AuditoriaNFe.Domain.Regras;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Domain.Documentos;

/// <summary>
/// Arquivo recebido em uma execução, embutido na execução (data-model §3). Guarda a identificação,
/// as validações e as regras aplicadas, nunca o conteúdo do XML (FR-037a).
/// </summary>
public sealed partial class ArquivoRecebido
{
    public const int TamanhoMaximoNome = 255;
    public const string MotivoLayoutInvalido = "layout inválido";

    private List<RegraAplicada> _regrasAplicadas = [];

    private ArquivoRecebido()
    {
        Nome = string.Empty;
        HashConteudo = string.Empty;
        ValidacaoLayout = new ValidacaoLayout();
        ValidacaoConteudo = EstadoValidacao.NaoRealizada("não avaliada");
    }

    public ArquivoRecebido(string nome, long tamanhoBytes, string hashConteudo)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tamanhoBytes);
        if (hashConteudo is null || !Sha256Hex().IsMatch(hashConteudo))
        {
            throw new ArgumentException("Hash deve ser SHA-256 hexadecimal minúsculo.", nameof(hashConteudo));
        }

        Nome = SanearNome(nome);
        TamanhoBytes = tamanhoBytes;
        HashConteudo = hashConteudo;
        ValidacaoLayout = new ValidacaoLayout();
        ValidacaoConteudo = EstadoValidacao.NaoRealizada("não avaliada");
    }

    /// <summary>Saneado, sem caminho.</summary>
    public string Nome { get; private set; }

    /// <summary>Id informado pela SOFIA (<c>documentos[].idDocumento</c>), até 128 caracteres.</summary>
    public string? IdDocumento { get; private set; }

    public long TamanhoBytes { get; private set; }

    /// <summary>SHA-256 dos bytes recebidos (FR-009).</summary>
    public string HashConteudo { get; private set; }

    public string? ElementoRaiz { get; private set; }
    public string? VersaoLayout { get; private set; }
    public string? ModeloIde { get; private set; }
    public string? ModeloChave { get; private set; }
    public string? ChaveAcesso { get; private set; }

    public ValidacaoLayout ValidacaoLayout { get; private set; }

    /// <summary><c>NaoRealizada — layout inválido</c> quando o layout falhou (FR-015).</summary>
    public EstadoValidacao ValidacaoConteudo { get; private set; }

    public IReadOnlyList<RegraAplicada> RegrasAplicadas => _regrasAplicadas;

    /// <summary>Arquivo anterior do lote, ou "sessão", quando o documento é duplicado (ERR-011).</summary>
    public string? DuplicadoDe { get; private set; }

    /// <summary>Documento gravado na base da sessão a partir deste arquivo.</summary>
    public Guid? DocumentoId { get; private set; }

    public void DefinirIdDocumento(string? idDocumento)
    {
        if (idDocumento is { Length: > 128 } or { Length: 0 })
        {
            throw new ArgumentException("idDocumento deve ter de 1 a 128 caracteres.", nameof(idDocumento));
        }

        IdDocumento = idDocumento;
    }

    public void RegistrarIdentificacao(IdentificacaoDocumento identificacao)
    {
        ArgumentNullException.ThrowIfNull(identificacao);
        ElementoRaiz = identificacao.ElementoRaiz;
        VersaoLayout = identificacao.VersaoLayout;
        ModeloIde = identificacao.ModeloIde;
        ModeloChave = identificacao.ModeloChave;
        ChaveAcesso = identificacao.ChaveAcesso;
    }

    public void RegistrarValidacaoLayout(ValidacaoLayout validacao)
    {
        ArgumentNullException.ThrowIfNull(validacao);
        ValidacaoLayout = validacao;
        if (validacao.Situacao != SituacaoValidacao.Realizada)
        {
            ValidacaoConteudo = EstadoValidacao.NaoRealizada(validacao.Motivo ?? MotivoLayoutInvalido);
        }
    }

    /// <summary>Layout validado com erros estruturais: o conteúdo não é validado (FR-015).</summary>
    public void RegistrarLayoutInvalido() => ValidacaoConteudo = EstadoValidacao.NaoRealizada(MotivoLayoutInvalido);

    public void RegistrarValidacaoConteudo(IReadOnlyList<RegraAplicada> regrasAplicadas)
    {
        ArgumentNullException.ThrowIfNull(regrasAplicadas);
        _regrasAplicadas = [.. regrasAplicadas];
        ValidacaoConteudo = EstadoValidacao.Realizada;
    }

    public void RegistrarConteudoNaoRealizado(string motivo) => ValidacaoConteudo = EstadoValidacao.NaoRealizada(motivo);

    public void MarcarDuplicado(string origem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origem);
        DuplicadoDe = origem;
    }

    public void AssociarDocumento(Guid documentoId)
    {
        if (documentoId == Guid.Empty)
        {
            throw new ArgumentException("Documento inválido.", nameof(documentoId));
        }

        DocumentoId = documentoId;
    }

    /// <summary>Só o nome, sem diretório nem caracteres de controle, com até 255 caracteres.</summary>
    public static string SanearNome(string? nome)
    {
        var semCaminho = (nome ?? string.Empty).Replace('\\', '/');
        semCaminho = semCaminho[(semCaminho.LastIndexOf('/') + 1)..];
        var limpo = new StringBuilder(semCaminho.Length);
        foreach (var caractere in semCaminho.Where(c => !char.IsControl(c)))
        {
            limpo.Append(caractere);
        }

        var resultado = limpo.ToString().Trim();
        if (resultado.Length == 0)
        {
            resultado = "arquivo-sem-nome.xml";
        }

        return resultado.Length > TamanhoMaximoNome ? resultado[..TamanhoMaximoNome] : resultado;
    }

    [GeneratedRegex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Hex();
}

/// <summary>
/// Validação de layout de um arquivo (data-model §3): pacote vigente, aplicado e tentados, data de
/// referência e convivência (FR-010a a FR-010f).
/// </summary>
public sealed class ValidacaoLayout
{
    private List<string> _pacotesTentados = [];

    public ValidacaoLayout()
    {
        Situacao = SituacaoValidacao.NaoRealizada;
        Motivo = "não avaliada";
    }

    public SituacaoValidacao Situacao { get; private set; }

    /// <summary>Preenchido em <see cref="SituacaoValidacao.NaoRealizada"/>.</summary>
    public string? Motivo { get; private set; }

    public DateOnly? DataReferencia { get; private set; }
    public OrigemDataReferencia? OrigemDataReferencia { get; private set; }
    public string? PacoteVigente { get; private set; }
    public string? PacoteAplicado { get; private set; }
    public IReadOnlyList<string> PacotesTentados => _pacotesTentados;
    public bool EmConvivencia { get; private set; }

    public EstadoValidacao Estado => Situacao == SituacaoValidacao.Realizada
        ? EstadoValidacao.Realizada
        : EstadoValidacao.NaoRealizada(Motivo ?? "não avaliada");

    public static ValidacaoLayout NaoRealizada(string motivo, DateOnly? dataReferencia = null, OrigemDataReferencia? origem = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        return new ValidacaoLayout
        {
            Situacao = SituacaoValidacao.NaoRealizada,
            Motivo = motivo,
            DataReferencia = dataReferencia,
            OrigemDataReferencia = origem,
        };
    }

    public static ValidacaoLayout Realizada(
        DateOnly dataReferencia,
        OrigemDataReferencia origem,
        string pacoteVigente,
        string pacoteAplicado,
        IReadOnlyList<string> pacotesTentados,
        bool emConvivencia)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pacoteVigente);
        ArgumentException.ThrowIfNullOrWhiteSpace(pacoteAplicado);
        ArgumentNullException.ThrowIfNull(pacotesTentados);
        return new ValidacaoLayout
        {
            Situacao = SituacaoValidacao.Realizada,
            Motivo = null,
            DataReferencia = dataReferencia,
            OrigemDataReferencia = origem,
            PacoteVigente = pacoteVigente,
            PacoteAplicado = pacoteAplicado,
            _pacotesTentados = [.. pacotesTentados],
            EmConvivencia = emConvivencia,
        };
    }
}
