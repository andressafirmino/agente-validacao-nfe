using AuditoriaNFe.Tests.Integracao.Infra;

namespace AuditoriaNFe.Tests.Rag;

/// <summary>PostgreSQL com <c>infra/postgres/init</c> (inclui extensão vector e schema rag).</summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoRagPostgres : ICollectionFixture<RagPostgresIntegracaoFixture>
{
    public const string Nome = "RagPostgres";
}

public sealed class RagPostgresIntegracaoFixture : IAsyncLifetime
{
    private readonly ContainerPostgres _postgres = new();

    public string ConnectionString => _postgres.ConnectionString;

    public async ValueTask InitializeAsync() => await _postgres.InitializeAsync();

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();
}
