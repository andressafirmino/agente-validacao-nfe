using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using Api.Agente.A2A;
using Api.Agente.Hospedagem;
using Api.Agente.Seguranca;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>
/// TS-076 (FR-052h, ADR-0014): o Agent Card e as rotas A2A exigem o Bearer da credencial de serviço
/// da SOFIA, comparado exatamente; sem a configuração, o agente não inicia.
/// </summary>
public sealed class CredencialServicoSofiaTests
{
    /// <summary>Valor sintético, usado só nos testes.</summary>
    public const string Chave = "chave-sintetica-de-servico-sofia-0123456789";

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    public static HttpClient ClienteAutenticado(WebApplicationFactory<Program> fabrica)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Chave);
        return cliente;
    }

    [Theory]
    [InlineData("GET", FabricaAgentCard.Rota)]
    [InlineData("GET", "/.WELL-KNOWN/agent-card.json")]
    [InlineData("GET", FabricaAgentCard.Rota + "/")]
    [InlineData("POST", FabricaAgentCard.RotaA2A)]
    [InlineData("POST", "/A2A/auditoria-nfe")]
    [InlineData("POST", "/a2a")]
    public async Task Sem_cabecalho_recebe_401_com_desafio_Bearer(string metodo, string rota)
    {
        await using var fabrica = CriarFabrica();
        using var cliente = fabrica.CreateClient();

        using var resposta = await cliente.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), rota), Cancelamento);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("Bearer", Assert.Single(resposta.Headers.WwwAuthenticate).Scheme);
        Assert.Empty(await resposta.Content.ReadAsByteArrayAsync(Cancelamento));
    }

    public static TheoryData<string> CabecalhosInvalidos() =>
    [
        "Bearer chave-incorreta",
        $"Bearer {Chave.ToUpperInvariant()}",
        $"Bearer {Chave}x",
        $"Bearer {Chave[..^1]}",
        $"Basic {Chave}",
        $"Token {Chave}",
        Chave,
        "Bearer",
        "Bearer ",
        $"Bearer{Chave}",
    ];

    [Theory]
    [MemberData(nameof(CabecalhosInvalidos))]
    public async Task Cabecalho_malformado_ou_chave_incorreta_recebe_401(string autorizacao)
    {
        await using var fabrica = CriarFabrica();
        using var cliente = fabrica.CreateClient();

        foreach (var (metodo, rota) in new[] { (HttpMethod.Get, FabricaAgentCard.Rota), (HttpMethod.Post, FabricaAgentCard.RotaA2A) })
        {
            using var requisicao = new HttpRequestMessage(metodo, rota);
            requisicao.Headers.TryAddWithoutValidation("Authorization", autorizacao);

            using var resposta = await cliente.SendAsync(requisicao, Cancelamento);

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }
    }

    [Theory]
    [InlineData("?access_token=" + Chave)]
    [InlineData("?token=" + Chave)]
    public async Task Chave_na_url_nao_substitui_o_cabecalho(string consulta)
    {
        await using var fabrica = CriarFabrica();
        using var cliente = fabrica.CreateClient();

        using var resposta = await cliente.GetAsync(FabricaAgentCard.Rota + consulta, Cancelamento);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Theory]
    [InlineData("Bearer")]
    [InlineData("bearer")]
    [InlineData("BEARER")]
    public async Task Card_com_a_chave_exata_responde_200_e_declara_o_esquema_sem_o_valor(string esquema)
    {
        await using var fabrica = CriarFabrica();
        using var cliente = fabrica.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, FabricaAgentCard.Rota);
        requisicao.Headers.TryAddWithoutValidation("Authorization", $"{esquema} {Chave}");

        using var resposta = await cliente.SendAsync(requisicao, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync(Cancelamento);
        Assert.Contains("\"securitySchemes\":{\"sofiaService\":{\"type\":\"http\",\"scheme\":\"bearer\"}}", corpo, StringComparison.Ordinal);
        Assert.Contains("\"security\":[{\"sofiaService\":[]}]", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, corpo, StringComparison.Ordinal);
        Assert.DoesNotContain(CredencialServicoSofiaOpcoes.ChaveToken, corpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rota_A2A_autenticada_segue_para_o_roteamento()
    {
        await using var fabrica = CriarFabrica();
        using var cliente = ClienteAutenticado(fabrica);

        using var resposta = await cliente.PostAsync(FabricaAgentCard.RotaA2A, new StringContent("{}"), Cancelamento);

        // Endpoint JSON-RPC ainda não mapeado (Fase 8): passa pela credencial e cai no 404.
        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Vivacidade_nao_exige_a_credencial()
    {
        await using var fabrica = CriarFabrica();
        using var cliente = fabrica.CreateClient();

        using var resposta = await cliente.GetAsync(SomenteAgentCardExtensions.RotaVivacidade, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" " + Chave)]
    [InlineData(Chave + "\n")]
    public void Configuracao_ausente_ou_invalida_impede_a_inicializacao(string? valor)
    {
        using var fabrica = CriarFabrica(chave: valor);

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.Services);

        var mensagens = Mensagens(erro);
        Assert.Contains(CredencialServicoSofiaOpcoes.ChaveToken, mensagens, StringComparison.Ordinal);
        Assert.DoesNotContain(Chave, mensagens, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logs_nao_trazem_a_chave_nem_a_tentativa_incorreta()
    {
        const string Tentativa = "tentativa-incorreta-sintetica-9876543210";
        var coletor = new ColetorLogs();
        await using var fabrica = CriarFabrica(logs: coletor);
        using var cliente = fabrica.CreateClient();

        foreach (var autorizacao in new[] { $"Bearer {Chave}", $"Bearer {Tentativa}" })
        {
            foreach (var (metodo, rota) in new[] { (HttpMethod.Get, FabricaAgentCard.Rota), (HttpMethod.Post, FabricaAgentCard.RotaA2A) })
            {
                using var requisicao = new HttpRequestMessage(metodo, rota);
                requisicao.Headers.TryAddWithoutValidation("Authorization", autorizacao);
                using var _ = await cliente.SendAsync(requisicao, Cancelamento);
            }
        }

        var linhas = coletor.Linhas();
        Assert.NotEmpty(linhas);
        Assert.DoesNotContain(linhas, l => l.Contains(Chave, StringComparison.Ordinal));
        Assert.DoesNotContain(linhas, l => l.Contains(Tentativa, StringComparison.Ordinal));
    }

    private static WebApplicationFactory<Program> CriarFabrica(string? chave = Chave, ColetorLogs? logs = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting(HospedagemOpcoes.ChaveModo, nameof(ModoHospedagem.SomenteAgentCard));
            builder.UseSetting($"{A2AOpcoes.Secao}:{nameof(A2AOpcoes.UrlPublica)}", "https://agente-auditoria-nfe.example");
            if (chave is not null)
            {
                builder.UseSetting(CredencialServicoSofiaOpcoes.ChaveToken, chave);
            }

            if (logs is not null)
            {
                builder.UseSetting("Logging:LogLevel:Default", "Trace");
                builder.ConfigureServices(s => s.AddSingleton<ILoggerProvider>(logs));
            }
        });

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

    /// <summary>Guarda mensagem, valores do estado e exceção de cada log.</summary>
    private sealed class ColetorLogs : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _linhas = new();

        public ILogger CreateLogger(string categoryName) => new Registrador(this);

        public List<string> Linhas() => [.. _linhas];

        public void Dispose()
        {
        }

        private sealed class Registrador(ColetorLogs coletor) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var valores = state is IEnumerable<KeyValuePair<string, object?>> pares
                    ? string.Join(" | ", pares.Select(p => $"{p.Key}={p.Value}"))
                    : string.Empty;
                coletor._linhas.Enqueue($"{formatter(state, exception)} | {valores} | {exception}");
            }
        }
    }
}
