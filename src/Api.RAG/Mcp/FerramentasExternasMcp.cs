using System.ComponentModel;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace Api.RAG.Mcp;

[McpServerToolType]
public static class FerramentasExternasMcp
{
  public const string NomeFerramenta = "listar_capacidades_externas_v1";

  [McpServerTool(Name = NomeFerramenta), Description(
      "Lista integrações externas disponíveis além da base de manuais (etapa 6). Na v1 não há delegação.")]
  public static RespostaCapacidades ListarCapacidadesExternas() =>
      new("0.1", []);
}

public sealed record RespostaCapacidades(
    [property: JsonPropertyName("versaoContrato")] string VersaoContrato,
    [property: JsonPropertyName("capacidades")] IReadOnlyList<string> Capacidades);
