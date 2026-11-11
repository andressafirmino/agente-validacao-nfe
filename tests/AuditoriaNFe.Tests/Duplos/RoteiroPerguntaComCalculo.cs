using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace AuditoriaNFe.Tests.Duplos;

/// <summary>Monta <see cref="RespostasIa.ComPergunta"/> a partir do retorno de <c>calcular_total</c> no roteiro.</summary>
public static class RoteiroPerguntaComCalculo
{
    public static string RespostaAposCalcularTotal(IReadOnlyList<ChatMessage> mensagens)
    {
        var json = JsonObjetoResultadoCalculo(mensagens);
        var calculoId = Guid.Parse(json["calculoId"]!.GetValue<string>());
        var resultado = json["resultado"]!.GetValue<string>();
        var excluidos = ExtrairDocumentosExcluidos(json);
        return RespostasIa.ComPergunta(
            calculoId,
            resultado,
            "O total da nota foi obtido com calcular_total; veja valoresCitados.",
            excluidos);
    }

    private static JsonObject JsonObjetoResultadoCalculo(IReadOnlyList<ChatMessage> mensagens)
    {
        foreach (var mensagem in mensagens.Reverse())
        {
            foreach (var parte in mensagem.Contents)
            {
                if (parte is FunctionResultContent resultado
                    && JsonNode.Parse(TextoResultadoFerramenta(resultado)) is JsonObject json
                    && json["calculoId"] is not null)
                {
                    return json;
                }
            }
        }

        throw new InvalidOperationException("resultado de calcular_total não encontrado nas mensagens.");
    }

    private static IEnumerable<JsonObject> ExtrairDocumentosExcluidos(JsonObject resultadoCalculo)
    {
        if (resultadoCalculo["documentosExcluidos"] is not JsonArray array)
        {
            return [];
        }

        return array
            .OfType<JsonObject>()
            .Select(d => new JsonObject
            {
                ["chaveAcesso"] = d["chaveAcesso"]?.GetValue<string>(),
                ["motivo"] = d["motivo"]?.GetValue<string>(),
            });
    }

    private static string TextoResultadoFerramenta(FunctionResultContent resultado) =>
        resultado.Result switch
        {
            JsonElement json => json.GetRawText(),
            string texto => texto,
            null => "{}",
            var valor => JsonSerializer.Serialize(valor),
        };
}
