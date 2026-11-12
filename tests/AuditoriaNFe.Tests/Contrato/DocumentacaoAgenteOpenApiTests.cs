using System.Net;
using System.Text.Json;
using Api.Agente.Documentacao;
using Api.Agente.Identificacao;
using AuditoriaNFe.Tests.Integracao.Infra;

namespace AuditoriaNFe.Tests.Contrato;

[Collection(ColecaoIntegracao.Nome)]
public sealed class DocumentacaoAgenteOpenApiTests(AmbienteIntegracao ambiente)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("post", "/api/v1/execucoes")]
    [InlineData("get", "/api/v1/contexto-usuario")]
    public async Task Operacoes_api_v1_expõem_cabecalhos_de_identificacao(string metodo, string rota)
    {
        using var documento = await ObterOpenApiAsync();

        var operacao = documento.RootElement
            .GetProperty("paths")
            .GetProperty(rota)
            .GetProperty(metodo);

        var parametros = operacao.GetProperty("parameters").EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(LeitorIdentificacaoHttp.CabecalhoUsuario, parametros);
        Assert.Contains(LeitorIdentificacaoHttp.CabecalhoUsuarioNome, parametros);
        Assert.Contains(LeitorIdentificacaoHttp.CabecalhoChamador, parametros);

        foreach (var nome in new[] { LeitorIdentificacaoHttp.CabecalhoUsuario, LeitorIdentificacaoHttp.CabecalhoChamador })
        {
            var parametro = operacao.GetProperty("parameters").EnumerateArray()
                .Single(p => string.Equals(p.GetProperty("name").GetString(), nome, StringComparison.OrdinalIgnoreCase));
            var obrigatorio = parametro.TryGetProperty("required", out var requerido) && requerido.GetBoolean();
            Assert.False(obrigatorio);
        }
    }

    [Fact]
    public async Task Post_execucoes_openapi_descreve_multipart_e_identificacao()
    {
        using var documento = await ObterOpenApiAsync();

        var operacao = documento.RootElement
            .GetProperty("paths")
            .GetProperty("/api/v1/execucoes")
            .GetProperty("post");

        var descricaoCorpo = operacao.GetProperty("requestBody").GetProperty("description").GetString() ?? string.Empty;
        Assert.Contains("Pergunta", descricaoCorpo, StringComparison.OrdinalIgnoreCase);

        var propriedades = operacao
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("multipart/form-data")
            .GetProperty("schema")
            .GetProperty("properties");

        foreach (var campo in new[] { "pergunta", "arquivos", "sessaoId", "correlacaoId" })
        {
            Assert.True(propriedades.TryGetProperty(campo, out _), $"Campo multipart ausente: {campo}");
        }

        var id = operacao.GetProperty("parameters").EnumerateArray()
            .Single(p => string.Equals(p.GetProperty("name").GetString(), LeitorIdentificacaoHttp.CabecalhoUsuario, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Id do usuário.", id.GetProperty("description").GetString());
        Assert.Equal(DocumentacaoAgenteOpenApiExtensions.ExemploUsuarioId, id.GetProperty("example").GetString());
    }

    private async Task<JsonDocument> ObterOpenApiAsync()
    {
        using var cliente = ambiente.CriarCliente();
        using var resposta = await cliente.GetAsync(DocumentacaoApiExtensions.RotaOpenApi, Cancelamento);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        await using var fluxo = await resposta.Content.ReadAsStreamAsync(Cancelamento);
        return await JsonDocument.ParseAsync(fluxo, cancellationToken: Cancelamento);
    }
}
