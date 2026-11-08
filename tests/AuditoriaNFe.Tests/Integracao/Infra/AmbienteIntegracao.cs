using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Domain.Sessoes;
using AuditoriaNFe.Infrastructure.Ia;
using AuditoriaNFe.Tests.Duplos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuditoriaNFe.Tests.Integracao.Infra;

/// <summary>Testes que compartilham o <see cref="AmbienteIntegracao"/>; rodam em sequência.</summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoIntegracao : ICollectionFixture<AmbienteIntegracao>
{
    public const string Nome = "Integracao";
}

/// <summary>
/// MongoDB e PostgreSQL descartáveis com os scripts de <c>infra/</c> e o host em
/// <c>Development</c> com <see cref="ChatClientRoteirizado"/> no lugar do provedor (passando pelo
/// mesmo pipeline de telemetria e consumo), <see cref="RelogioControlado"/> e diretório de
/// aprovações temporário (research R-23).
/// </summary>
public sealed class AmbienteIntegracao : IAsyncLifetime
{
    public const string ProvedorRoteirizado = "OpenAI";

    private readonly List<WebApplicationFactory<Program>> _fabricas = [];

    public ContainerMongo Mongo { get; } = new();
    public ContainerPostgres Postgres { get; } = new();
    public ChatClientRoteirizado ChatClient { get; } = new();
    public RelogioControlado Relogio { get; } = new();
    public string DiretorioAprovacoes { get; } = Path.Combine(Path.GetTempPath(), $"aprovacoes-{Guid.NewGuid():N}");

    /// <summary>Host padrão, em <c>Development</c>.</summary>
    public WebApplicationFactory<Program> Fabrica { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(Mongo.InitializeAsync().AsTask(), Postgres.InitializeAsync().AsTask());
        Fabrica = CriarFabrica();
    }

    public HttpClient CriarCliente() => Fabrica.CreateClient();

    /// <summary>
    /// Outro host com ambiente, configurações e serviços próprios (aplicados por último); descartado
    /// com o ambiente. Com <paramref name="provedorReal"/>, usa <c>Ia:Provedor=OpenAI</c> com a chave
    /// e o modelo das variáveis de ambiente, sem o <see cref="ChatClientRoteirizado"/> (avaliação).
    /// </summary>
    public WebApplicationFactory<Program> CriarFabrica(
        string ambiente = "Development",
        IReadOnlyDictionary<string, string?>? configuracoes = null,
        Action<IServiceCollection>? servicos = null,
        bool provedorReal = false)
    {
        var fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(ambiente);
            builder.UseSetting("ConnectionStrings:MongoDb", Mongo.ConnectionString);
            builder.UseSetting("ConnectionStrings:Consumo", Postgres.ConnectionString);
            builder.UseSetting("Ia:Provedor", provedorReal ? FabricaChatClient.NomeProvedorOpenAI : "Simulado");
            builder.UseSetting("Persistencia:AplicarMigracoes", "true");
            builder.UseSetting("Aprovacao:Diretorio", DiretorioAprovacoes);
            builder.UseSetting("LimiteTaxa:PermissoesPorJanela", "10000");
            foreach (var (chave, valor) in configuracoes ?? new Dictionary<string, string?>())
            {
                builder.UseSetting(chave, valor);
            }

            builder.ConfigureTestServices(colecao =>
            {
                colecao.RemoveAll<IRelogio>();
                colecao.AddSingleton<IRelogio>(Relogio);
                if (provedorReal)
                {
                    return;
                }

                colecao.RemoveAll<IChatClient>();
                colecao.AddSingleton(provedor => provedor.GetRequiredService<FabricaChatClient>()
                    .Montar(ChatClient, ProvedorRoteirizado, OrigemValoresConsumo.InformadoProvedor));
            });
            if (servicos is not null)
            {
                builder.ConfigureTestServices(servicos);
            }
        });

        _fabricas.Add(fabrica);
        return fabrica;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var fabrica in _fabricas)
        {
            await fabrica.DisposeAsync();
        }

        await Mongo.DisposeAsync();
        await Postgres.DisposeAsync();
        if (Directory.Exists(DiretorioAprovacoes))
        {
            Directory.Delete(DiretorioAprovacoes, recursive: true);
        }
    }
}
