using Api.RAG.Configuracao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
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

                document.Servers =
                [
                    new OpenApiServer
                    {
                        Url = "/",
                        Description = "Mesma origem da barra de endereços (use https:// no Vercel).",
                    },
                ];

                foreach (var operacao in EnumerarOperacoes(document))
                {
                    if (RequerSeguridadeAdmin(operacao))
                    {
                        AplicarRequisitoBearer(operacao, document);
                    }
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>Marca a operação no OpenAPI com <c>security: [ { "Bearer": [] } ]</c> (Swagger Authorize).</summary>
    public static TBuilder AplicarSegurancaBearerAdminOpenApi<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddOpenApiOperationTransformer((operation, context, _) =>
        {
            AplicarRequisitoBearer(operation, context.Document);
            return Task.CompletedTask;
        });

    /// <summary>Corpo multipart no Swagger (POST ingestao/pdf; metadados em <c>api-rag-ingestao.md</c>).</summary>
    public static TBuilder AplicarCorpoMultipartIngestaoPdfOpenApi<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            operation.RequestBody = CorpoMultipartIngestaoPdfOpenApi();
            return Task.CompletedTask;
        });

    private static OpenApiRequestBody CorpoMultipartIngestaoPdfOpenApi() => new()
    {
        Required = true,
        Description =
            "PDF oficial + metadados de ingestão. Obrigatórios: arquivo, documentoIdExterno, titulo, versaoDocumento, vigenciaInicio (yyyy-MM-dd).",
        Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
        {
            ["multipart/form-data"] = new()
            {
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                    {
                        ["arquivo"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Format = "binary",
                            Description = "PDF (application/pdf).",
                        },
                        ["documentoIdExterno"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            MaxLength = 128,
                            Description = "Id estável do documento na base.",
                        },
                        ["titulo"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            MaxLength = 512,
                        },
                        ["versaoDocumento"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            MaxLength = 32,
                        },
                        ["vigenciaInicio"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Format = "date",
                            Description = "Data inicial de vigência (yyyy-MM-dd).",
                        },
                        ["vigenciaFim"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Format = "date",
                        },
                        ["tipoDocumento"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Description = "Padrão: NF-e.",
                        },
                        ["modelo"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Description = "Padrão: 55.",
                        },
                        ["versaoLayout"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Description = "Padrão: 4.00.",
                        },
                        ["regrasRelacionadas"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Description = "IDs de regra separados por vírgula (ex.: W16-10,W04-10).",
                        },
                        ["fonte"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            MaxLength = 256,
                            Description = "Proveniência; padrão = nome do arquivo.",
                        },
                    },
                    Required = new HashSet<string>(StringComparer.Ordinal)
                    {
                        "arquivo",
                        "documentoIdExterno",
                        "titulo",
                        "versaoDocumento",
                        "vigenciaInicio",
                    },
                },
            },
        },
    };

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

    private static IEnumerable<OpenApiOperation> EnumerarOperacoes(OpenApiDocument document)
    {
        if (document.Paths is null)
        {
            yield break;
        }

        foreach (var caminho in document.Paths.Values)
        {
            if (caminho.Operations is null)
            {
                continue;
            }

            foreach (var operacao in caminho.Operations.Values)
            {
                yield return operacao;
            }
        }
    }

    private static bool RequerSeguridadeAdmin(OpenApiOperation operacao) =>
        operacao.Tags?.Any(tag => string.Equals(tag.Name, "Ingestao", StringComparison.OrdinalIgnoreCase)) == true
        && operacao.OperationId is not "NavegarIngestaoRag";

    private static void AplicarRequisitoBearer(OpenApiOperation operation, OpenApiDocument? document)
    {
        if (document is null)
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes.TryAdd(EsquemaBearer, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "Chave admin (`Rag__ChaveAdministracao` no Vercel).",
        });

        var referencia = new OpenApiSecuritySchemeReference(EsquemaBearer, document, externalResource: null);
        operation.Security =
        [
            new OpenApiSecurityRequirement { [referencia] = [] },
        ];
    }
}
