using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace AuditoriaNFe.Application.ContextProviders;

/// <summary>
/// Dado não confiável entre marcas, como JSON com <c>&lt;</c> e <c>&gt;</c> escapados, o que impede
/// forjar as marcas (P07, FR-053; research R-07).
/// </summary>
public static class TextoDelimitado
{
    public static JsonSerializerOptions OpcoesJson { get; } = CriarOpcoes();

    public static string Montar<T>(string inicio, string fim, T dado) =>
        new StringBuilder()
            .AppendLine(inicio)
            .AppendLine(JsonSerializer.Serialize(dado, OpcoesJson))
            .AppendLine(fim)
            .ToString();

    private static JsonSerializerOptions CriarOpcoes()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        opcoes.MakeReadOnly();
        return opcoes;
    }
}
