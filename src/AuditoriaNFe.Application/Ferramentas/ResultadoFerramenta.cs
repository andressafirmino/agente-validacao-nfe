using System.Text.Json.Serialization;

namespace AuditoriaNFe.Application.Ferramentas;

/// <summary>Envelope de erro interno das ferramentas (contrato §1).</summary>
public sealed record ErroFerramenta(string Codigo, string Mensagem);

public static class CodigoErroFerramenta
{
    public const string ArgumentoInvalido = "ArgumentoInvalido";
    public const string DocumentoNaoEncontrado = "DocumentoNaoEncontrado";
    public const string CampoNaoCatalogado = "CampoNaoCatalogado";
    public const string NaoAutorizada = "NaoAutorizada";
    public const string Timeout = "Timeout";
    public const string FalhaInterna = "FalhaInterna";
}

public static class ResultadoFerramenta
{
    public static object Erro(string codigo, string mensagem) =>
        new Dictionary<string, object>
        {
            ["erro"] = new Dictionary<string, string> { ["codigo"] = codigo, ["mensagem"] = mensagem },
        };
}
