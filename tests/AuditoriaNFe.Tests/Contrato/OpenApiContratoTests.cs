using System.Text.Json;
using Api.Agente.Documentacao;
using Api.Agente.Endpoints;
using AuditoriaNFe.Tests.Integracao.Infra;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>
/// R-20: o documento gerado por Microsoft.AspNetCore.OpenApi em <c>Development</c> é equivalente a
/// contracts/api-agente.openapi.yaml nas operações já entregues: rota, método, <c>operationId</c>,
/// códigos de resposta e campos do multipart. As linhas da teoria crescem com as fases.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class OpenApiContratoTests(AmbienteIntegracao ambiente)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("post", "/api/v1/execucoes")]
    [InlineData("get", "/api/v1/execucoes/{execucaoId}")]
    [InlineData("delete", "/api/v1/sessoes/{sessaoId}")]
    [InlineData("get", "/api/v1/contexto-usuario")]
    [InlineData("delete", "/api/v1/contexto-usuario")]
    [InlineData("put", "/api/v1/contexto-usuario/preferencias")]
    [InlineData("delete", "/api/v1/contexto-usuario/cnpjs/{cnpj}")]
    [InlineData("delete", "/api/v1/contexto-usuario/achados-recorrentes/{chave}")]
    public async Task Operacao_gerada_e_igual_a_do_contrato(string metodo, string rota)
    {
        var esperada = Assert.Single(ContratoOpenApiYaml.Operacoes(), o => o.Metodo == metodo && o.Rota == rota);
        using var documento = await GerarAsync();

        Assert.True(documento.RootElement.GetProperty("paths").TryGetProperty(rota, out var caminho), $"Rota ausente no documento gerado: {rota}");
        Assert.True(caminho.TryGetProperty(metodo, out var operacao), $"Método ausente: {metodo} {rota}");
        Assert.Equal(esperada.OperationId, operacao.GetProperty("operationId").GetString());
        Assert.Equal(esperada.Status.Order(), operacao.GetProperty("responses").EnumerateObject().Select(r => r.Name).Order());
        Assert.Equal(esperada.CamposMultipart.Order(), CamposMultipart(operacao).Order());
    }

    [Fact]
    public async Task Documento_gerado_nao_tem_operacao_de_api_fora_do_contrato()
    {
        var contrato = ContratoOpenApiYaml.Operacoes().Select(o => $"{o.Metodo} {o.Rota}").ToHashSet(StringComparer.Ordinal);
        using var documento = await GerarAsync();

        var geradas = documento.RootElement.GetProperty("paths").EnumerateObject()
            .Where(p => p.Name.StartsWith(RotasApiV1.Prefixo, StringComparison.Ordinal))
            .SelectMany(p => p.Value.EnumerateObject().Select(m => $"{m.Name} {p.Name}"))
            .ToList();

        Assert.NotEmpty(geradas);
        Assert.All(geradas, operacao => Assert.Contains(operacao, contrato));
    }

    [Fact]
    public void Contrato_lido_tem_as_operacoes_do_plano()
    {
        var operacoes = ContratoOpenApiYaml.Operacoes();

        var criar = Assert.Single(operacoes, o => o.OperationId == "criarExecucao");
        Assert.Equal(["200", "400", "404", "410", "413", "429", "503"], criar.Status);
        Assert.Equal(["pergunta", "arquivos", "sessaoId", "correlacaoId"], criar.CamposMultipart);
        Assert.Equal(["200", "404"], Assert.Single(operacoes, o => o.OperationId == "obterExecucao").Status);
    }

    private async Task<JsonDocument> GerarAsync()
    {
        using var cliente = ambiente.CriarCliente();
        var json = await cliente.GetStringAsync(DocumentacaoApiExtensions.RotaOpenApi, Cancelamento);
        return JsonDocument.Parse(json);
    }

    private static IEnumerable<string> CamposMultipart(JsonElement operacao)
    {
        if (!operacao.TryGetProperty("requestBody", out var corpo)
            || !corpo.GetProperty("content").TryGetProperty("multipart/form-data", out var multipart))
        {
            return [];
        }

        return Propriedades(multipart.GetProperty("schema"));
    }

    private static IEnumerable<string> Propriedades(JsonElement schema)
    {
        var nomes = schema.TryGetProperty("properties", out var propriedades)
            ? propriedades.EnumerateObject().Select(p => p.Name).ToList()
            : [];
        foreach (var composicao in new[] { "allOf", "anyOf" })
        {
            if (schema.TryGetProperty(composicao, out var partes))
            {
                nomes.AddRange(partes.EnumerateArray().SelectMany(Propriedades));
            }
        }

        return nomes.Distinct(StringComparer.Ordinal);
    }
}
