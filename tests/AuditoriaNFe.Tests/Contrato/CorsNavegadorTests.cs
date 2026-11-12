using System.Net;
using Api.Agente.Endpoints;
using Api.Agente.Identificacao;
using Api.Agente.Seguranca;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>TS-070 (FR-052e): política <c>SofiaBrowser</c> só nas rotas <c>/api/v1</c>.</summary>
public sealed class CorsNavegadorTests
{
    private const string OrigemPermitida = "http://localhost:4200";
    private const string OrigemDesconhecida = "https://site-nao-autorizado.example";
    private const string RotaApi = RotasApiV1.Prefixo + "/teste";
    private const string RotaForaDoGrupo = "/.well-known/agent-card.json";

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Preflight_de_origem_configurada_recebe_permissao_sem_credenciais()
    {
        await using var app = await IniciarAsync(OrigemPermitida);
        using var cliente = app.GetTestClient();

        using var resposta = await cliente.SendAsync(
            Preflight(RotaApi, OrigemPermitida, "POST", "content-type,x-usuario-id,x-usuario-nome,x-chamador-id"),
            Cancelamento);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(OrigemPermitida, Cabecalho(resposta, "Access-Control-Allow-Origin"));
        Assert.Contains("POST", Cabecalho(resposta, "Access-Control-Allow-Methods"), StringComparison.Ordinal);
        var cabecalhos = Cabecalho(resposta, "Access-Control-Allow-Headers")!;
        Assert.Contains("content-type", cabecalhos, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LeitorIdentificacaoHttp.CabecalhoUsuario, cabecalhos, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LeitorIdentificacaoHttp.CabecalhoUsuarioNome, cabecalhos, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LeitorIdentificacaoHttp.CabecalhoChamador, cabecalhos, StringComparison.OrdinalIgnoreCase);
        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Preflight_nao_permite_cabecalho_fora_da_politica()
    {
        await using var app = await IniciarAsync(OrigemPermitida);
        using var cliente = app.GetTestClient();

        using var resposta = await cliente.SendAsync(Preflight(RotaApi, OrigemPermitida, "POST", "authorization"), Cancelamento);

        Assert.DoesNotContain("authorization", Cabecalho(resposta, "Access-Control-Allow-Headers") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preflight_de_origem_nao_configurada_nao_recebe_permissao()
    {
        await using var app = await IniciarAsync(OrigemPermitida);
        using var cliente = app.GetTestClient();

        using var resposta = await cliente.SendAsync(Preflight(RotaApi, OrigemDesconhecida, "POST"), Cancelamento);

        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Lista_vazia_nao_concede_permissao_a_nenhuma_origem()
    {
        await using var app = await IniciarAsync();
        using var cliente = app.GetTestClient();

        using var preflight = await cliente.SendAsync(Preflight(RotaApi, OrigemPermitida, "POST"), Cancelamento);
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaApi);
        requisicao.Headers.Add("Origin", OrigemPermitida);
        using var resposta = await cliente.SendAsync(requisicao, Cancelamento);

        Assert.Null(Cabecalho(preflight, "Access-Control-Allow-Origin"));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData(RotaForaDoGrupo)]
    [InlineData("/health/live")]
    public async Task Rotas_fora_do_grupo_api_v1_ficam_sem_CORS(string rota)
    {
        await using var app = await IniciarAsync(OrigemPermitida);
        using var cliente = app.GetTestClient();

        using var preflight = await cliente.SendAsync(Preflight(rota, OrigemPermitida, "GET"), Cancelamento);
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.Add("Origin", OrigemPermitida);
        using var resposta = await cliente.SendAsync(requisicao, Cancelamento);

        Assert.Null(Cabecalho(preflight, "Access-Control-Allow-Origin"));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Requisicao_sem_Origin_segue_o_pipeline_normal_sem_cabecalhos_CORS()
    {
        await using var app = await IniciarAsync(OrigemPermitida);
        using var cliente = app.GetTestClient();

        using var resposta = await cliente.GetAsync(RotaApi, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.DoesNotContain(resposta.Headers, cabecalho => cabecalho.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Requisicao_de_origem_configurada_recebe_permissao_e_Retry_After_exposto()
    {
        await using var app = await IniciarAsync(OrigemPermitida, "https://sofia.example");
        using var cliente = app.GetTestClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, RotaApi);
        requisicao.Headers.Add("Origin", OrigemPermitida);

        using var resposta = await cliente.SendAsync(requisicao, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(OrigemPermitida, Cabecalho(resposta, "Access-Control-Allow-Origin"));
        Assert.Contains("Retry-After", Cabecalho(resposta, "Access-Control-Expose-Headers"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Origin", resposta.Headers.Vary);
        Assert.Null(Cabecalho(resposta, "Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://localhost:4200/")]
    [InlineData("http://localhost:4200/app")]
    [InlineData("http://localhost:4200?x=1")]
    [InlineData("localhost:4200")]
    [InlineData("ftp://localhost:4200")]
    [InlineData("http://*.example.com")]
    [InlineData("http://usuario:senha@localhost:4200")]
    public void Origem_invalida_impede_a_inicializacao(string origem)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => CorsNavegadorExtensions.LerOrigens(Configuracao(origem)));

        Assert.Contains("Cors:AllowedOrigins", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Entradas_vazias_sao_ignoradas_e_repetidas_aparecem_uma_vez()
    {
        var origens = CorsNavegadorExtensions.LerOrigens(Configuracao("", "  ", OrigemPermitida, OrigemPermitida, "https://sofia.example:8443"));

        Assert.Equal([OrigemPermitida, "https://sofia.example:8443"], origens);
    }

    private static async Task<WebApplication> IniciarAsync(params string[] origens)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddConfiguration(Configuracao(origens));
        builder.Services.AdicionarCorsNavegador(builder.Configuration);
        var app = builder.Build();
        app.UseRouting();
        app.UseCors();
        app.MapearApiV1().MapMethods("/teste", [HttpMethods.Get, HttpMethods.Post], () => Results.Ok());
        app.MapGet(RotaForaDoGrupo, () => Results.Ok());
        app.MapGet("/health/live", () => Results.Ok());
        await app.StartAsync(Cancelamento);
        return app;
    }

    private static IConfiguration Configuracao(params string[] origens) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(origens.Select((origem, indice) =>
                new KeyValuePair<string, string?>($"{CorsNavegadorExtensions.Secao}:{indice}", origem)))
            .Build();

    private static HttpRequestMessage Preflight(string rota, string origem, string metodo, string? cabecalhos = null)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Options, rota);
        requisicao.Headers.Add("Origin", origem);
        requisicao.Headers.Add("Access-Control-Request-Method", metodo);
        if (cabecalhos is not null)
        {
            requisicao.Headers.Add("Access-Control-Request-Headers", cabecalhos);
        }

        return requisicao;
    }

    private static string? Cabecalho(HttpResponseMessage resposta, string nome) =>
        resposta.Headers.TryGetValues(nome, out var valores) ? string.Join(",", valores) : null;
}
