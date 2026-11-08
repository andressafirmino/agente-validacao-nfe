using System.Text.Json.Nodes;

namespace AuditoriaNFe.Tests.Duplos;

/// <summary>
/// Respostas do provedor no formato de <c>contracts/contrato-ia-v1.schema.json</c>, montadas em JSON
/// para não depender do tipo C# do contrato.
/// </summary>
public static class RespostasIa
{
    public const string ResumoPadrao = "Análise concluída sobre os documentos recebidos.";

    public static string Valida(
        string resumo = ResumoPadrao,
        IEnumerable<JsonObject>? indicios = null,
        IEnumerable<JsonObject>? ausencias = null,
        JsonObject? resposta = null,
        string versaoContrato = "1.0") =>
        new JsonObject
        {
            ["versaoContrato"] = versaoContrato,
            ["indicios"] = Lista(indicios),
            ["ausenciasEvidencia"] = Lista(ausencias),
            ["resposta"] = resposta,
            ["foraDeEscopo"] = false,
            ["informacaoIndisponivel"] = false,
            ["recomendacoes"] = new JsonArray(),
            ["resumo"] = resumo,
        }.ToJsonString();

    public static JsonObject Indicio(string descricao, string severidade, IEnumerable<JsonObject> evidencias, IEnumerable<Guid>? achadosRelacionados = null) =>
        new()
        {
            ["descricao"] = descricao,
            ["severidade"] = severidade,
            ["evidencias"] = Lista(evidencias),
            ["achadosRelacionados"] = new JsonArray([.. (achadosRelacionados ?? []).Select(id => (JsonNode)id.ToString())]),
        };

    public static JsonObject Ausencia(string pontoAvaliado, string motivo) =>
        new() { ["pontoAvaliado"] = pontoAvaliado, ["motivo"] = motivo };

    public static JsonObject EvidenciaCampoXml(string chaveAcesso, string caminho, string valor) =>
        Evidencia("CampoXml", chaveAcesso: chaveAcesso, caminho: caminho, valor: valor);

    public static JsonObject EvidenciaRegra(string regraId) => Evidencia("RegraCatalogo", regraId: regraId);

    public static JsonObject Evidencia(
        string tipo,
        string? chaveAcesso = null,
        string? caminho = null,
        string? valor = null,
        string? regraId = null,
        string? chunkId = null,
        string? calculoId = null) => new()
    {
        ["tipo"] = tipo,
        ["chaveAcesso"] = chaveAcesso,
        ["caminho"] = caminho,
        ["valor"] = valor,
        ["regraId"] = regraId,
        ["chunkId"] = chunkId,
        ["calculoId"] = calculoId,
    };

    private static JsonArray Lista(IEnumerable<JsonObject>? itens) => new([.. (itens ?? []).Select(i => (JsonNode)i)]);
}
