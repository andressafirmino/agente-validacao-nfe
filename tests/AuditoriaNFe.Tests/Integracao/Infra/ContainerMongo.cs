using AuditoriaNFe.Tests.Arquitetura;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace AuditoriaNFe.Tests.Integracao.Infra;

/// <summary>
/// MongoDB descartável com o script de <c>infra/mongodb/init</c> e credenciais geradas a cada execução.
/// Pronto só depois do fim da inicialização da imagem (o servidor temporário também responde).
/// </summary>
public sealed class ContainerMongo : IAsyncLifetime
{
    public const string Imagem = "mongo:8.0.32";
    private const int Porta = 27017;

    private readonly string _usuarioApp = $"app{Guid.NewGuid():N}"[..12];
    private readonly string _senhaApp = Guid.NewGuid().ToString("N");
    private readonly IContainer _container;

    public ContainerMongo()
    {
        _container = new ContainerBuilder(Imagem)
            .WithEnvironment("MONGO_INITDB_ROOT_USERNAME", "root")
            .WithEnvironment("MONGO_INITDB_ROOT_PASSWORD", Guid.NewGuid().ToString("N"))
            .WithEnvironment("MONGO_APP_USERNAME", _usuarioApp)
            .WithEnvironment("MONGO_APP_PASSWORD", _senhaApp)
            .WithResourceMapping(new DirectoryInfo(Path.Combine(Repositorio.Raiz(), "infra", "mongodb", "init")), "/docker-entrypoint-initdb.d/")
            .WithPortBinding(Porta, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("MongoDB init process complete")
                .UntilCommandIsCompleted("mongosh", "--quiet", "--eval", "db.adminCommand('ping').ok"))
            .Build();
    }

    public string ConnectionString =>
        $"mongodb://{_usuarioApp}:{_senhaApp}@{_container.Hostname}:{_container.GetMappedPublicPort(Porta)}/auditoria_agente?authSource=auditoria_agente";

    public async ValueTask InitializeAsync() => await LogContainer.IniciarAsync(_container, Imagem);

    /// <summary>Para o contêiner, simulando o armazenamento indisponível (TS-064).</summary>
    public Task PararAsync(CancellationToken cancelamento = default) => _container.StopAsync(cancelamento);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
