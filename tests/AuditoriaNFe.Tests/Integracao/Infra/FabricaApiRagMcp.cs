using System.Net;
using System.Net.Sockets;
using Api.RAG;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AuditoriaNFe.Tests.Integracao.Infra;

/// <summary>Sobe a Api.RAG com Kestrel em porta TCP real para o cliente MCP do agente.</summary>
public sealed class ServidorApiRagMcp : IAsyncDisposable
{
    private readonly int _porta = ObterPortaLivre();
    private WebApplication? _app;

    public string BaseUrl => $"http://127.0.0.1:{_porta}";

    public string EndpointMcp => $"{BaseUrl}/mcp";

    public async Task IniciarAsync(
        string? connectionStringRag = null,
        string? chaveAdministracao = null,
        Action<IServiceCollection>? personalizarServicos = null,
        IReadOnlyDictionary<string, string?>? variaveisAmbiente = null,
        CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return;
        }

        var raiz = Path.GetDirectoryName(typeof(ApiRagMarcador).Assembly.Location)!;
        var url = BaseUrl;
        if (connectionStringRag is not null)
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Rag", connectionStringRag);
        }

        if (chaveAdministracao is not null)
        {
            Environment.SetEnvironmentVariable("Rag__ChaveAdministracao", chaveAdministracao);
        }

        Environment.SetEnvironmentVariable("Rag__ModoRecuperacao", "Semente");
        Environment.SetEnvironmentVariable("Rag__PermitirFallbackSemente", "true");

        if (variaveisAmbiente is not null)
        {
            foreach (var (chave, valor) in variaveisAmbiente)
            {
                Environment.SetEnvironmentVariable(chave, valor);
            }
        }

        _app = ApiRagHosting.CriarAplicacao(
            new WebApplicationOptions
            {
                ContentRootPath = raiz,
                EnvironmentName = Environments.Development,
            },
            [url],
            personalizarServicos);
        await _app.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
            _app = null;
        }
    }

    private static int ObterPortaLivre()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var porta = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return porta;
    }
}
