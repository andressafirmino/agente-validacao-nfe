using System.Text.Json;
using System.Text.Json.Nodes;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Tests.Arquitetura;
using AuditoriaNFe.Tests.Duplos;
using Microsoft.Extensions.AI;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>
/// R-05: o schema que <c>RunAsync&lt;ContratoIaV1&gt;</c> envia ao provedor, gerado pelo tipo, é o de
/// contracts/contrato-ia-v1.schema.json. A comparação resolve referências e ignora anotações
/// (<c>description</c>, <c>title</c>, <c>$id</c>), que não mudam a validação.
/// </summary>
public sealed class ContratoIaV1SchemaTests
{
    private static readonly string[] Anotacoes = ["$schema", "$id", "$defs", "$comment", "title", "description"];

    public static string CaminhoPublicado =>
        Path.Combine(Repositorio.Raiz(), "specs", "001-agente-auditoria-nfe", "contracts", "contrato-ia-v1.schema.json");

    [Fact]
    public void Schema_gerado_pelo_tipo_e_igual_ao_publicado()
    {
        var formato = ChatResponseFormat.ForJsonSchema<ContratoIaV1>(ContratoIaV1.OpcoesJson);
        var gerado = JsonNode.Parse(formato.Schema!.Value.GetRawText())!;
        var publicado = JsonNode.Parse(File.ReadAllText(CaminhoPublicado))!;

        Assert.Equal(Texto(Normalizar(publicado)), Texto(Normalizar(gerado)));
    }

    [Fact]
    public void Resposta_valida_do_duplo_de_teste_e_lida_com_todos_os_campos()
    {
        var json = RespostasIa.Valida(
            resumo: "Resumo.",
            indicios: [RespostasIa.Indicio("Indício.", "Media", [RespostasIa.EvidenciaRegra("W04-10")])],
            ausencias: [RespostasIa.Ausencia("Ponto.", "Sem trecho.")]);

        var contrato = JsonSerializer.Deserialize<ContratoIaV1>(json, ContratoIaV1.OpcoesJson)!;

        Assert.Equal(ContratoIa.Versao, contrato.VersaoContrato);
        var indicio = Assert.Single(contrato.Indicios);
        Assert.Equal(("RegraCatalogo", "W04-10"), (Assert.Single(indicio.Evidencias).Tipo, indicio.Evidencias[0].RegraId));
        Assert.Single(contrato.AusenciasEvidencia);
        Assert.Null(contrato.Resposta);
        Assert.Equal("Resumo.", contrato.Resumo);
    }

    [Theory]
    [InlineData("""{"versaoContrato":"1.0","indicios":[],"ausenciasEvidencia":[],"resposta":null,"foraDeEscopo":false,"informacaoIndisponivel":false,"recomendacoes":[],"resumo":"x","execucaoId":"0193f0c2-7000-7000-8000-000000000101"}""")]
    [InlineData("""{"versaoContrato":"1.0","indicios":[],"ausenciasEvidencia":[],"resposta":null,"foraDeEscopo":false,"informacaoIndisponivel":false,"recomendacoes":[]}""")]
    public void Propriedade_extra_ou_ausente_e_rejeitada_na_leitura(string json)
    {
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<ContratoIaV1>(json, ContratoIaV1.OpcoesJson));
    }

    private static string Texto(JsonNode no) => no.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Resolve <c>$ref</c> (ponteiros JSON na raiz), remove anotações, une <c>anyOf</c> com null e ordena listas sem ordem.</summary>
    private static JsonNode Normalizar(JsonNode raiz) => Normalizar(raiz, raiz, 0);

    private static JsonNode Normalizar(JsonNode no, JsonNode raiz, int profundidade)
    {
        Assert.True(profundidade < 64, "Referência recursiva no schema.");
        switch (no)
        {
            case JsonArray lista:
                return new JsonArray([.. lista.Select(item => item is null ? null : Normalizar(item, raiz, profundidade + 1))]);
            case JsonObject objeto when objeto["$ref"] is JsonValue referencia:
                return Normalizar(Resolver(raiz, referencia.GetValue<string>()), raiz, profundidade + 1);
            case JsonObject objeto:
            {
                var resultado = new JsonObject();
                foreach (var (chave, valor) in objeto.Where(p => !Anotacoes.Contains(p.Key)))
                {
                    resultado[chave] = chave == "properties" && valor is JsonObject propriedades
                        ? new JsonObject(propriedades.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, p.Value is null ? null : Normalizar(p.Value, raiz, profundidade + 1))))
                        : valor is null ? null : Normalizar(valor, raiz, profundidade + 1);
                }

                return Simplificar(resultado);
            }

            default:
                return no.DeepClone();
        }
    }

    private static JsonNode Simplificar(JsonObject objeto)
    {
        if (objeto["anyOf"] is JsonArray { Count: 2 } alternativas
            && alternativas.SingleOrDefault(a => a?["type"]?.GetValueKind() == JsonValueKind.String && a["type"]!.GetValue<string>() == "null") is { } nulo
            && alternativas.Single(a => !ReferenceEquals(a, nulo)) is JsonObject outro
            && outro["type"] is JsonValue tipo)
        {
            var unido = (JsonObject)outro.DeepClone();
            unido["type"] = new JsonArray(tipo.GetValue<string>(), "null");
            foreach (var (chave, valor) in objeto.Where(p => p.Key != "anyOf"))
            {
                unido[chave] = valor?.DeepClone();
            }

            objeto = unido;
        }

        foreach (var chave in new[] { "type", "required", "enum" })
        {
            if (objeto[chave] is JsonArray lista)
            {
                objeto[chave] = new JsonArray([.. lista.Select(v => v!.ToJsonString()).Order(StringComparer.Ordinal).Select(v => JsonNode.Parse(v))]);
            }
        }

        return new JsonObject(objeto.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
    }

    private static JsonNode Resolver(JsonNode raiz, string referencia)
    {
        Assert.StartsWith("#", referencia, StringComparison.Ordinal);
        var atual = raiz;
        foreach (var parte in referencia.TrimStart('#').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var segmento = parte.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            atual = atual is JsonArray lista ? lista[int.Parse(segmento, System.Globalization.CultureInfo.InvariantCulture)]! : atual[segmento]!;
        }

        return atual;
    }
}
