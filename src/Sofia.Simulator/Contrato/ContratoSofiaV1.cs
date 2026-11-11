using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sofia.Simulator.Contrato;

public static class ContratoSofiaV1
{
    public const string Versao = "1.0";
    public const string NomeArtifact = "analise-sofia-v1";

    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed record SolicitacaoAnaliseSofia(
    string VersaoContrato,
    Guid IdExecucao,
    Guid IdSolicitacao,
    UsuarioSofia Usuario,
    string Pergunta,
    IReadOnlyList<DocumentoSofia> Documentos,
    ContextoSofiaMensagem? Contexto = null);

public sealed record UsuarioSofia(string Nome);

public sealed record DocumentoSofia(string IdDocumento, string Nome, string Texto, string? Tipo = null);

public sealed record ContextoSofiaMensagem(string? Idioma = null, string? Instrucoes = null);

public sealed record ResultadoAnaliseSofia(
    string VersaoContrato,
    Guid IdExecucao,
    Guid IdSolicitacao,
    AgenteSofia Agente,
    string Status,
    string Analise,
    IReadOnlyList<AchadoSofia> Achados,
    IReadOnlyList<string>? Observacoes = null,
    ErroSofia? Erro = null);

public sealed record AgenteSofia(string IdAgente, string Nome);

public sealed record AchadoSofia(
    string IdAchado,
    string Descricao,
    IReadOnlyList<string> IdDocumentos,
    IReadOnlyList<string> Evidencias,
    string? Severidade = null);

public sealed record ErroSofia(string Codigo, string Mensagem);
