using System.Text.Json;

namespace AuditoriaNFe.Tests.Avaliacao;

/// <summary>Estrutura do conjunto de avaliacao de perguntas (T119, trait sob demanda).</summary>
public sealed class AvaliacaoTests
{
  private static readonly string Caminho = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "Avaliacao", "conjunto-avaliacao.v1.json");

  [Fact]
  [Trait("Categoria", "Avaliacao")]
  public void Conjunto_tem_50_perguntas_com_ids_unicos()
  {
    var caminho = Path.GetFullPath(Caminho);
    using var doc = JsonDocument.Parse(File.ReadAllText(caminho));
    var perguntas = doc.RootElement.GetProperty("perguntas");
    Assert.True(perguntas.GetArrayLength() >= 50);
    var ids = perguntas.EnumerateArray().Select(p => p.GetProperty("id").GetString()).ToList();
    Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
  }
}
