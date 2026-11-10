using System.Net;
using Api.Agente.Documentacao;
using Api.Agente.Hospedagem;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AuditoriaNFe.Tests.Integracao;

[Collection(ColecaoIntegracao.Nome)]
public sealed class HostTests(AmbienteIntegracao ambiente)
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Host_fora_de_Development_sem_autenticacao_nao_inicia(string nomeAmbiente)
    {
        var fabrica = ambiente.CriarFabrica(nomeAmbiente, new Dictionary<string, string?> { ["Seguranca:AutenticacaoHabilitada"] = "false" });

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.Services);

        Assert.Contains("D-05", Mensagens(erro), StringComparison.Ordinal);
    }

    [Fact]
    public void Host_fora_de_Development_com_autenticacao_tambem_nao_inicia_enquanto_vigorar_o_D05()
    {
        var fabrica = ambiente.CriarFabrica("Production", new Dictionary<string, string?> { ["Seguranca:AutenticacaoHabilitada"] = "true" });

        Assert.ThrowsAny<Exception>(() => fabrica.Services);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData(DocumentacaoApiExtensions.RotaOpenApi)]
    public async Task Documentacao_existe_em_Development(string rota)
    {
        using var cliente = ambiente.CriarCliente();

        var resposta = await cliente.GetAsync(rota, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Theory]
    [InlineData("Production", "/swagger/index.html")]
    [InlineData("Production", DocumentacaoApiExtensions.RotaOpenApi)]
    [InlineData("Staging", DocumentacaoApiExtensions.RotaOpenApi)]
    public async Task Documentacao_nao_existe_fora_de_Development_no_modo_completo(string nomeAmbiente, string rota)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = nomeAmbiente });
        builder.WebHost.UseTestServer();
        builder.Services.AdicionarDocumentacaoApi(builder.Environment, ModoHospedagem.Completo);
        await using var app = builder.Build();
        app.UsarDocumentacaoApi(ModoHospedagem.Completo);
        await app.StartAsync(Cancelamento);
        using var cliente = app.GetTestClient();

        var resposta = await cliente.GetAsync(rota, Cancelamento);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Saude_responde_sem_dados(string rota)
    {
        using var cliente = ambiente.CriarCliente();

        var resposta = await cliente.GetAsync(rota, Cancelamento);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync(Cancelamento));
    }

    [Fact]
    public async Task Prontidao_com_dependencia_indisponivel_responde_503_sem_detalhes_e_vivacidade_continua_ok()
    {
        var fabrica = ambiente.CriarFabrica(configuracoes: new Dictionary<string, string?>
        {
            ["ConnectionStrings:MongoDb"] = "mongodb://127.0.0.1:1/auditoria_agente",
            ["Persistencia:TimeoutMongo"] = "00:00:02",
        });
        using var cliente = fabrica.CreateClient();

        var prontidao = await cliente.GetAsync("/health/ready", Cancelamento);
        var vivacidade = await cliente.GetAsync("/health/live", Cancelamento);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, prontidao.StatusCode);
        Assert.Equal("Unhealthy", await prontidao.Content.ReadAsStringAsync(Cancelamento));
        Assert.Equal(HttpStatusCode.OK, vivacidade.StatusCode);
    }

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
}
