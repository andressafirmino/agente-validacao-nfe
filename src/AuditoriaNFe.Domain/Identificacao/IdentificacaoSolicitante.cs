using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AuditoriaNFe.Domain.Identificacao;

/// <summary>
/// Identidade informada pelo chamador, sem verificação na versão 1 (desvio D-05; data-model §0;
/// FR-052a, FR-052c).
/// </summary>
public sealed partial class IdentificacaoSolicitante
{
    public const string UsuarioAnonimo = "anonimo";
    public const string ChamadorNaoInformado = "nao-informado";
    public const int TamanhoMaximo = 128;

    // Caractere fora do formato aceito: a concatenação não é ambígua.
    internal const char Separador = '|';
    public const string PrefixoUsuarioPorNome = "nome-sha256:";

    private IdentificacaoSolicitante(string usuarioId, string chamadorId, string? usuarioNome = null)
    {
        UsuarioId = usuarioId;
        ChamadorId = chamadorId;
        UsuarioNome = usuarioNome;
        ChaveIsolamento = CalcularChave(chamadorId, usuarioId);
    }

    /// <summary>Dado pessoal: nunca em log.</summary>
    public string UsuarioId { get; }

    public string ChamadorId { get; }

      public bool UsuarioIdentificado => UsuarioId != UsuarioAnonimo;

    /// <summary>Nome recebido no A2A, sem espaços nas pontas; nunca em log (data-model §0).</summary>
    public string? UsuarioNome { get; }

    /// <summary>SHA-256 hexadecimal minúsculo de <c>chamadorId</c> + separador + <c>usuarioId</c>.</summary>
    public string ChaveIsolamento { get; }

    /// <summary>Valor ausente (nulo ou vazio) recebe o padrão; formato inválido devolve <c>false</c> (ERR-007).</summary>
    public static bool TentarCriar(
        string? usuarioId,
        string? chamadorId,
        [NotNullWhen(true)] out IdentificacaoSolicitante? identificacao)
    {
        var usuario = string.IsNullOrEmpty(usuarioId) ? UsuarioAnonimo : usuarioId;
        var chamador = string.IsNullOrEmpty(chamadorId) ? ChamadorNaoInformado : chamadorId;

        if (!FormatoValido(usuario) || !FormatoValido(chamador))
        {
            identificacao = null;
            return false;
        }

        identificacao = new IdentificacaoSolicitante(usuario, chamador);
        return true;
    }

    public static IdentificacaoSolicitante Criar(string? usuarioId, string? chamadorId) =>
        TentarCriar(usuarioId, chamadorId, out var identificacao)
            ? identificacao
            : throw new ArgumentException("Identificação do usuário ou do chamador em formato inválido.");

    /// <summary>Identificação A2A a partir do nome do usuário (FR-052d, data-model §0).</summary>
    public static bool TentarCriarPorNome(string? nome, string chamadorPadrao, [NotNullWhen(true)] out IdentificacaoSolicitante? identificacao)
    {
        var usuarioNome = nome?.Trim();
        if (string.IsNullOrEmpty(usuarioNome) || usuarioNome.Length > 200)
        {
            identificacao = null;
            return false;
        }

        if (!FormatoValido(chamadorPadrao))
        {
            identificacao = null;
            return false;
        }

        var normalizado = NormalizarNome(usuarioNome);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizado)));
        var usuarioId = $"{PrefixoUsuarioPorNome}{hash}";
        identificacao = new IdentificacaoSolicitante(usuarioId, chamadorPadrao, usuarioNome);
        return true;
    }

    /// <summary>Unicode NFC, espaços nas pontas, internos reduzidos a um, minúsculas invariantes.</summary>
    public static string NormalizarNome(string nome)
    {
        var nfc = nome.Normalize(NormalizationForm.FormC).Trim();
        return EspacosInternos().Replace(nfc, " ").ToLowerInvariant();
    }

    public static bool FormatoValido(string valor) => Formato().IsMatch(valor);

    private static string CalcularChave(string chamadorId, string usuarioId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{chamadorId}{Separador}{usuarioId}"));
        return Convert.ToHexStringLower(bytes);
    }

    [GeneratedRegex(@"\A[A-Za-z0-9._:@-]{1,128}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosInternos();
}
