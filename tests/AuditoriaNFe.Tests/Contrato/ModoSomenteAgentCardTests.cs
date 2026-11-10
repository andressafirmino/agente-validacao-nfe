using System.Net;
using System.Text.Json.Nodes;
using Api.Agente.A2A;
using Api.Agente.Documentacao;
using Api.Agente.Hospedagem;
using Api.Agente.Seguranca;
using AuditoriaNFe.Tests.Arquitetura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>TS-071 (FR-052b, FR-052f, D-08): fora de <c>Development</c>, só o Agent Card e a vivacidade.</summary>
public sealed class ModoSomenteAgentCardTests
{
    private const string UrlPublica = "https://agente-auditoria-nfe.example";

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Card_em_Production_traz_a_url_publica_o_idAgente_e_a_implantacao()
    {
        await using var fabrica = CriarFabrica();
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(fabrica);

        using var resposta = await cliente.GetAsync(FabricaAgentCard.Rota, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);
        var card = JsonNode.Parse(await resposta.Content.ReadAsStringAsync(Cancelamento))!;
        Assert.Equal($"{UrlPublica}{FabricaAgentCard.RotaA2A}", (string?)card["url"]);
        Assert.Equal("0.3.0", (string?)card["protocolVersion"]);
        Assert.Equal("JSONRPC", (string?)card["preferredTransport"]);
        Assert.False((bool)card["capabilities"]!["streaming"]!);
        Assert.False((bool)card["capabilities"]!["pushNotifications"]!);
        var parametros = card["capabilities"]!["extensions"]![0]!["params"]!;
        Assert.Equal("agente-auditoria-nfe", (string?)parametros["idAgente"]);
        Assert.Equal("1.0", (string?)parametros["versaoContrato"]);
        Assert.Equal(FabricaAgentCard.ImplantacaoSomenteAgentCard, (string?)parametros["implantacao"]);
        Assert.Equal("analisar_documentos", (string?)card["skills"]![0]!["id"]);
        Assert.Equal("bearer", (string?)card["securitySchemes"]!["sofiaService"]!["scheme"]);
    }

    [Fact]
    public async Task Card_e_o_exemplo_do_contrato_acrescido_da_implantacao()
    {
        await using var fabrica = CriarFabrica(urlPublica: "http://localhost:5310/");
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(fabrica);
        var exemplo = JsonNode.Parse(await File.ReadAllTextAsync(
            Path.Combine(Repositorio.Raiz(), "specs", "001-agente-auditoria-nfe", "contracts", "exemplos", "agent-card-v03.json"), Cancelamento));

        var card = JsonNode.Parse(await cliente.GetStringAsync(FabricaAgentCard.Rota, Cancelamento))!;
        var parametros = card["capabilities"]!["extensions"]![0]!["params"]!.AsObject();
        Assert.True(parametros.Remove("implantacao"));

        Assert.True(JsonNode.DeepEquals(exemplo, card), card.ToJsonString());
    }

    [Fact]
    public async Task Vivacidade_responde_sem_dependencias()
    {
        await using var fabrica = CriarFabrica();
        using var cliente = fabrica.CreateClient();

        using var resposta = await cliente.GetAsync("/health/live", Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync(Cancelamento));
    }

    [Theory]
    [InlineData("GET", "/health/ready")]
    [InlineData("GET", "/swagger/index.html")]
    [InlineData("GET", DocumentacaoApiExtensions.RotaOpenApi)]
    [InlineData("GET", "/api/v1/execucoes")]
    [InlineData("POST", "/api/v1/execucoes")]
    [InlineData("POST", FabricaAgentCard.RotaA2A)]
    public async Task Rotas_alem_do_card_e_da_vivacidade_respondem_404(string metodo, string rota)
    {
        await using var fabrica = CriarFabrica();
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(fabrica);

        using var resposta = await cliente.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), rota), Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Card_nao_concede_permissao_CORS()
    {
        await using var fabrica = CriarFabrica();
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(fabrica);
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, FabricaAgentCard.Rota);
        requisicao.Headers.Add("Origin", "https://site.example");

        using var resposta = await cliente.SendAsync(requisicao, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void Production_no_modo_completo_nao_inicia()
    {
        using var fabrica = CriarFabrica(modo: nameof(ModoHospedagem.Completo));

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.Services);

        var mensagens = Mensagens(erro);
        Assert.Contains("D-05", mensagens, StringComparison.Ordinal);
        Assert.Contains("D-08", mensagens, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Qualquer")]
    [InlineData("5")]
    public void Modo_invalido_impede_a_inicializacao(string modo)
    {
        using var fabrica = CriarFabrica(modo: modo);

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.Services);

        Assert.Contains(HospedagemOpcoes.ChaveModo, Mensagens(erro), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("localhost:5310")]
    [InlineData("/relativa")]
    [InlineData("ftp://agente.example")]
    [InlineData("https://usuario:senha@agente.example")]
    [InlineData("https://agente.example/?x=1")]
    public void UrlPublica_invalida_impede_a_inicializacao(string urlPublica)
    {
        var erro = Assert.ThrowsAny<Exception>(() => CredencialServicoSofiaTests.ValidarInicializacao(
            (s, c) => s.AdicionarSomenteAgentCard(c),
            new Dictionary<string, string?> { [$"{A2AOpcoes.Secao}:{nameof(A2AOpcoes.UrlPublica)}"] = urlPublica }));

        Assert.Contains("A2A:UrlPublica", Mensagens(erro), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development", nameof(ModoHospedagem.Completo))]
    [InlineData("Development", nameof(ModoHospedagem.SomenteAgentCard))]
    [InlineData("Production", nameof(ModoHospedagem.SomenteAgentCard))]
    [InlineData("Staging", nameof(ModoHospedagem.SomenteAgentCard))]
    public void Guarda_aceita_Development_e_o_modo_restrito(string ambiente, string modo)
    {
        GuardaDesvioD05.Verificar(Ambiente(ambiente), Configuracao(modo));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Guarda_recusa_o_modo_completo_fora_de_Development(string ambiente)
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => GuardaDesvioD05.Verificar(Ambiente(ambiente), Configuracao(nameof(ModoHospedagem.Completo))));

        Assert.Contains("D-08", erro.Message, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CriarFabrica(
        string modo = nameof(ModoHospedagem.SomenteAgentCard),
        string urlPublica = UrlPublica) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting(HospedagemOpcoes.ChaveModo, modo);
            builder.UseSetting($"{A2AOpcoes.Secao}:{nameof(A2AOpcoes.UrlPublica)}", urlPublica);
            builder.UseSetting(CredencialServicoSofiaOpcoes.ChaveToken, CredencialServicoSofiaTests.Chave);
        });

    private static IConfiguration Configuracao(string modo) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [HospedagemOpcoes.ChaveModo] = modo })
            .Build();

    private static IHostEnvironment Ambiente(string nome) => new AmbienteTeste { EnvironmentName = nome };

    private static string Mensagens(Exception erro)
    {
        var mensagens = new List<string>();
        for (var atual = erro; atual is not null; atual = atual.InnerException)
        {
            mensagens.Add(atual.Message);
            if (atual is AggregateException agregada)
            {
                mensagens.AddRange(agregada.InnerExceptions.Select(e => e.Message));
            }
        }

        return string.Join(" | ", mensagens);
    }

    private sealed class AmbienteTeste : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Api.Agente";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
