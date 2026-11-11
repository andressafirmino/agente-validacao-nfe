using Api.RAG.Configuracao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Api.RAG.Infraestrutura;

/// <summary>Só para <c>dotnet ef migrations</c>.</summary>
internal sealed class RagDbContextFactory : IDesignTimeDbContextFactory<RagDbContext>
{
    public RagDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<RagDbContext>();
        RagDbContext.Configurar(builder, "Host=localhost;Database=auditoria_agente");
        return new RagDbContext(builder.Options);
    }
}
