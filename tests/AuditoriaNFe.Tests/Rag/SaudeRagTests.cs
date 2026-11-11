using System.Net;
using Api.RAG.Infraestrutura;
using AuditoriaNFe.Tests.Integracao.Infra;
using Microsoft.EntityFrameworkCore;

namespace AuditoriaNFe.Tests.Rag;

[Collection(ColecaoRagPostgres.Nome)]
public sealed class SaudeRagTests(RagPostgresIntegracaoFixture ambiente) : IAsyncLifetime
{
    private ServidorApiRagMcp? _servidor;

    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using (var contexto = CriarContexto())
        {
            await contexto.Database.MigrateAsync(Cancelamento);
        }

        _servidor = new ServidorApiRagMcp();
        await _servidor.IniciarAsync(ambiente.ConnectionString, cancellationToken: Cancelamento);
    }

    public async ValueTask DisposeAsync()
    {
        if (_servidor is not null)
        {
            await _servidor.DisposeAsync();
        }
    }

    private RagDbContext CriarContexto()
    {
        var builder = new DbContextOptionsBuilder<RagDbContext>();
        RagDbContext.Configurar(builder, ambiente.ConnectionString);
        return new RagDbContext(builder.Options);
    }

    [Fact]
    public async Task Health_ready_responde_200_com_postgres_disponivel()
    {
        using var cliente = new HttpClient { BaseAddress = new Uri(_servidor!.BaseUrl) };
        var resposta = await cliente.GetAsync("/health/ready", Cancelamento);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }
}
