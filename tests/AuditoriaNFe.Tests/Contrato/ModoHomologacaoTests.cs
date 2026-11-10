using System.Net;
using System.Text.Json.Nodes;
using Api.Agente.A2A;
using Api.Agente.Documentacao;
using Api.Agente.Endpoints;
using Api.Agente.Hospedagem;
using Api.Agente.Seguranca;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using static AuditoriaNFe.Tests.Integracao.Infra.AssercoesEnvelope;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>
/// TS-074 (FR-052g, FR-052b, FR-004, desvio D-09): em <c>Production</c>, o modo <c>Homologacao</c>
/// registra o agente completo com a documentação da API e só inicia com provedor de IA real,
/// migrações fora da inicialização e limites abaixo do corpo aceito pelo Vercel.
/// </summary>
[Collection(ColecaoIntegracao.Nome)]
public sealed class ModoHomologacaoTests
{
    private const string UrlPublica = "https://agente-auditoria-nfe-homolog.example";

    private readonly AmbienteIntegracao _ambiente;

    public ModoHomologacaoTests(AmbienteIntegracao ambiente)
    {
        _ambiente = ambiente;
        _ambiente.ChatClient.Limpar();
    }

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/swagger/index.html")]
    [InlineData(DocumentacaoApiExtensions.RotaOpenApi)]
    public async Task Configuracao_valida_inicia_com_documentacao_e_saude(string rota)
    {
        using var cliente = CriarFabrica().CreateClient();

        using var resposta = await cliente.GetAsync(rota, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public void Rotas_api_v1_sao_as_do_modo_completo()
    {
        var homologacao = RotasApiV1Mapeadas(CriarFabrica());

        Assert.NotEmpty(homologacao);
        Assert.Equal(RotasApiV1Mapeadas(_ambiente.Fabrica), homologacao);
    }

    [Fact]
    public async Task Card_declara_a_homologacao_com_a_url_publica_e_o_limite_do_lote()
    {
        using var cliente = CredencialServicoSofiaTests.ClienteAutenticado(CriarFabrica());

        using var resposta = await cliente.GetAsync(FabricaAgentCard.Rota, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var card = JsonNode.Parse(await resposta.Content.ReadAsStringAsync(Cancelamento))!;
        Assert.Equal($"{UrlPublica}{FabricaAgentCard.RotaA2A}", (string?)card["url"]);
        var parametros = card["capabilities"]!["extensions"]![0]!["params"]!;
        Assert.Equal(FabricaAgentCard.ImplantacaoHomologacao, (string?)parametros["implantacao"]);
        Assert.Equal(ValidadorModoHomologacao.TamanhoTotalMaximoBytes, (long)parametros["limites"]!["tamanhoTotalBytes"]!);
        Assert.Equal("bearer", (string?)card["securitySchemes"]!["sofiaService"]!["scheme"]);
    }

    /// <summary>FR-052h: card e rotas A2A exigem a credencial; a API REST continua aberta (D-09).</summary>
    [Theory]
    [InlineData("GET", FabricaAgentCard.Rota)]
    [InlineData("POST", FabricaAgentCard.RotaA2A)]
    public async Task Card_e_A2A_sem_credencial_recebem_401(string metodo, string rota)
    {
        using var cliente = CriarFabrica().CreateClient();

        using var resposta = await cliente.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), rota), Cancelamento);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task API_REST_continua_sem_a_credencial_da_SOFIA()
    {
        using var cliente = CriarFabrica().CreateClient();

        using var resposta = await cliente.GetAsync($"{RotasApiV1.Prefixo}/execucoes/{Guid.NewGuid()}", Cancelamento);

        Assert.NotEqual(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.False(resposta.Headers.Contains("WWW-Authenticate"));
    }

    [Fact]
    public void Homologacao_sem_a_credencial_da_SOFIA_nao_inicia()
    {
        var fabrica = CriarFabrica(new Dictionary<string, string?> { [CredencialServicoSofiaOpcoes.ChaveToken] = "" });

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.Services);

        Assert.Contains(CredencialServicoSofiaOpcoes.ChaveToken, Mensagens(erro), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lote_acima_de_4000000_bytes_recebe_ERR002()
    {
        var cliente = CriarFabrica().CreateClient();
        var conteudo = new byte[ValidadorModoHomologacao.TamanhoTotalMaximoBytes + 1];
        Array.Fill(conteudo, (byte)'a');

        var resposta = await ClienteExecucoes.CriarAsync(cliente, [new ArquivoEnvio("lote-grande.xml", conteudo)], cancelamento: Cancelamento);

        var envelope = Rejeitada(resposta, HttpStatusCode.RequestEntityTooLarge, CodigoErro.TamanhoExcedido);
        Assert.Equal(ValidadorModoHomologacao.TamanhoTotalMaximoBytes, envelope.Erro!.Detalhes!["limiteBytes"].GetInt64());
        Assert.Empty(_ambiente.ChatClient.Chamadas);
    }

    [Fact]
    public async Task Documento_OpenAPI_usa_https_informado_pelo_proxy()
    {
        using var cliente = CriarFabrica().CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, DocumentacaoApiExtensions.RotaOpenApi);
        requisicao.Headers.Add("X-Forwarded-Proto", "https");

        using var resposta = await cliente.SendAsync(requisicao, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var documento = JsonNode.Parse(await resposta.Content.ReadAsStringAsync(Cancelamento))!;
        var servidor = (string?)documento["servers"]![0]!["url"];
        Assert.StartsWith("https://", servidor, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ia:Provedor", "Simulado")]
    [InlineData("Persistencia:AplicarMigracoes", "true")]
    [InlineData("Limites:TamanhoTotalBytes", "4000001")]
    [InlineData("Limites:CorpoHttpBytes", "4500001")]
    public void Configuracao_invalida_impede_a_inicializacao(string chave, string valor)
    {
        var fabrica = CriarFabrica(new Dictionary<string, string?> { [chave] = valor });

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.Services);

        var mensagens = Mensagens(erro);
        Assert.Contains("D-09", mensagens, StringComparison.Ordinal);
        Assert.Contains(chave, mensagens, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Guarda_aceita_a_homologacao_fora_de_Development(string ambiente)
    {
        GuardaDesvioD05.Verificar(new AmbienteTeste { EnvironmentName = ambiente }, Configuracao(nameof(ModoHospedagem.Homologacao)));
    }

    [Fact]
    public void Guarda_continua_recusando_o_modo_completo_e_cita_os_desvios()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => GuardaDesvioD05.Verificar(
            new AmbienteTeste { EnvironmentName = Environments.Production }, Configuracao(nameof(ModoHospedagem.Completo))));

        Assert.Contains("D-05", erro.Message, StringComparison.Ordinal);
        Assert.Contains("D-08", erro.Message, StringComparison.Ordinal);
        Assert.Contains("D-09", erro.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Host em <c>Production</c> com a configuração da homologação (plan.md, "Homologação pública") e
    /// o dublê de IA do ambiente. As migrações vêm do host padrão em <c>Development</c>, porque a
    /// homologação não as aplica na inicialização.
    /// </summary>
    private WebApplicationFactory<Program> CriarFabrica(IReadOnlyDictionary<string, string?>? ajustes = null)
    {
        _ = _ambiente.Fabrica.Services;
        var configuracoes = new Dictionary<string, string?>
        {
            [HospedagemOpcoes.ChaveModo] = nameof(ModoHospedagem.Homologacao),
            ["Seguranca:AutenticacaoHabilitada"] = "false",
            ["Ia:Provedor"] = "OpenAI",
            ["OpenAI:ApiKey"] = "chave-ficticia-da-homologacao",
            ["OpenAI:Modelo"] = "modelo-ficticio",
            ["Persistencia:AplicarMigracoes"] = "false",
            ["Limites:TamanhoTotalBytes"] = "4000000",
            ["Limites:CorpoHttpBytes"] = "4500000",
            [$"{A2AOpcoes.Secao}:{nameof(A2AOpcoes.UrlPublica)}"] = UrlPublica,
            [CredencialServicoSofiaOpcoes.ChaveToken] = CredencialServicoSofiaTests.Chave,
        };
        foreach (var (chave, valor) in ajustes ?? new Dictionary<string, string?>())
        {
            configuracoes[chave] = valor;
        }

        return _ambiente.CriarFabrica(ambiente: Environments.Production, configuracoes: configuracoes);
    }

    private static string[] RotasApiV1Mapeadas(WebApplicationFactory<Program> fabrica) =>
        fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith(RotasApiV1.Prefixo, StringComparison.Ordinal) == true)
            .Select(e => $"{string.Join(',', e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {e.RoutePattern.RawText}")
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IConfiguration Configuracao(string modo) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [HospedagemOpcoes.ChaveModo] = modo })
            .Build();

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
