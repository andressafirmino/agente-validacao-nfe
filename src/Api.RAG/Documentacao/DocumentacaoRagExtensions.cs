using Api.RAG.Configuracao;
using Microsoft.OpenApi;

namespace Api.RAG.Documentacao;

/// <summary>Swagger UI com Bearer para rotas admin <c>/api/v1/rag</c> (ingestão manual no navegador).</summary>
public static class DocumentacaoRagExtensions
{
    public const string RotaOpenApi = "/openapi/v1.json";
    public const string RotaSwaggerUi = "/swagger";

    private const string EsquemaBearer = "Bearer";

    public static IServiceCollection AdicionarDocumentacaoRag(this IServiceCollection services, IConfiguration configuration)
    {
        if (!Habilitada(configuration))
        {
            return services;
        }

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Api.RAG",
                    Version = "v1",
                    Description =
                        "Ingestão e administração da base. Clique em **Authorize** e informe o token (valor de Rag__ChaveAdministracao).",
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[EsquemaBearer] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    Description = "Chave admin (`Rag__ChaveAdministracao` no Vercel).",
                };

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                if (context.Description.RelativePath?.StartsWith("api/v1/rag", StringComparison.OrdinalIgnoreCase) == true
                    && string.Equals(context.Description.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(EsquemaBearer)] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static WebApplication UsarDocumentacaoRag(this WebApplication app, IConfiguration configuration)
    {
        if (!Habilitada(configuration))
        {
            return app;
        }

        app.MapOpenApi();
        app.UseSwaggerUI(opcoes =>
        {
            opcoes.SwaggerEndpoint(RotaOpenApi, "Api.RAG v1");
            opcoes.RoutePrefix = "swagger";
            opcoes.DocumentTitle = "Api.RAG — ingestão";
            opcoes.EnablePersistAuthorization();
        });

        return app;
    }

    public static bool Habilitada(IConfiguration configuration) =>
        configuration.GetValue($"{RagOpcoes.Secao}:ExporDocumentacaoOpenApi", defaultValue: true);
}
