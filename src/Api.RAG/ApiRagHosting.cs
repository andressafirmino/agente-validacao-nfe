using Api.RAG.Configuracao;
using Api.RAG.Documentacao;
using Api.RAG.Endpoints;
using Api.RAG.Mcp;
using Api.RAG.Seguranca;
using Api.RAG.Servicos;
using ModelContextProtocol.AspNetCore;

namespace Api.RAG;

/// <summary>Composição compartilhada entre <c>Program</c> e testes de integração MCP.</summary>
public static class ApiRagHosting
{
    public static WebApplication CriarAplicacao(
        WebApplicationOptions? opcoes = null,
        string[]? urls = null,
        Action<IServiceCollection>? personalizarServicos = null)
    {
        var builder = WebApplication.CreateBuilder(opcoes ?? new WebApplicationOptions { Args = urls ?? [] });
        if (urls is { Length: > 0 })
        {
            builder.WebHost.UseUrls(urls);
        }

        ConfigurarServicos(builder);
        personalizarServicos?.Invoke(builder.Services);
        var app = builder.Build();
        app.UsarDocumentacaoRag(builder.Configuration);
        app.UsarProtecaoAdministracaoRag();
        MapearEndpoints(app);
        return app;
    }

    public static void ConfigurarServicos(WebApplicationBuilder builder)
    {
        builder.Services.AdicionarOpcoesRag(builder.Configuration);
        builder.Services.AdicionarDocumentacaoRag(builder.Configuration);
        builder.Services.AdicionarPersistenciaRag(builder.Configuration);
        builder.Services.AddSingleton<CatalogoTrechos>();
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithToolsFromAssembly(typeof(ApiRagMarcador).Assembly);
    }

    public static void MapearEndpoints(WebApplication app)
    {
        SaudeRagEndpoints.Mapear(app);
        IngestaoEndpoints.Mapear(app);
        IngestaoPdfEndpoints.Mapear(app);
        app.MapMcp("/mcp");
    }
}
