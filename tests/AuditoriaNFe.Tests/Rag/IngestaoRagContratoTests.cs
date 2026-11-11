using System.Text.Json;
using AuditoriaNFe.Tests.Arquitetura;
using Json.Schema;

namespace AuditoriaNFe.Tests.Rag;

public sealed class IngestaoRagContratoTests
{
    private static string PastaFixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Rag");
    private static string SchemaIngestao =>
        Path.Combine(Repositorio.Raiz(), "specs", "002-rag-pgvector", "contracts", "ingestao-rag-v1.schema.json");

    private static readonly Lazy<JsonSchema> Esquema = new(() => JsonSchema.FromFile(SchemaIngestao));

    [Fact]
    public void Fixture_sintetica_atende_o_schema()
    {
        var conteudo = File.ReadAllText(Path.Combine(PastaFixtures, "documento-sintetico.v1.json"));
        using var elemento = JsonDocument.Parse(conteudo);
        var resultado = Esquema.Value.Evaluate(elemento.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Flag });
        Assert.True(resultado.IsValid, string.Join("; ", resultado.Errors?.Select(e => e.Value) ?? []));
    }
}
