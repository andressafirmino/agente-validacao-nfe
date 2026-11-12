using Api.Agente.Identificacao;
using AuditoriaNFe.Domain.Identificacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Agente.Documentacao;

/// <summary>Cabeçalhos de identificação e multipart de execução no Swagger (homologação FR-052g).</summary>
public static class DocumentacaoAgenteOpenApiExtensions
{
    public const string ExemploUsuarioId = "homolog-01";
    public const string ExemploChamadorId = "teste-vercel";
    public const string ExemploUsuarioNome = "Maria Silva";

    /// <summary>Parâmetros de identificação em todas as rotas com <see cref="FiltroIdentificacao"/>.</summary>
    public static TBuilder AplicarIdentificacaoOpenApi<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddOpenApiOperationTransformer((operacao, _, _) =>
        {
            AnexarCabecalhosIdentificacao(operacao);
            return Task.CompletedTask;
        });

    /// <summary>Corpo multipart de <c>POST /api/v1/execucoes</c> para o Try it out.</summary>
    public static TBuilder AplicarCorpoMultipartExecucaoOpenApi<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddOpenApiOperationTransformer((operacao, _, _) =>
        {
            operacao.RequestBody = CorpoMultipartExecucaoOpenApi();
            return Task.CompletedTask;
        });

    private static void AnexarCabecalhosIdentificacao(OpenApiOperation operacao)
    {
        operacao.Parameters ??= [];
        var existentes = operacao.Parameters
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var parametro in CabecalhosIdentificacaoOpenApi())
        {
            if (existentes.Contains(parametro.Name))
            {
                continue;
            }

            operacao.Parameters.Add(parametro);
        }
    }

    internal static IReadOnlyList<OpenApiParameter> CabecalhosIdentificacaoOpenApi() =>
    [
        new()
        {
            Name = LeitorIdentificacaoHttp.CabecalhoUsuario,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Id do usuário.",
            Schema = EsquemaIdentificadorOpenApi(),
            Example = ExemploUsuarioId,
        },
        new()
        {
            Name = LeitorIdentificacaoHttp.CabecalhoUsuarioNome,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Nome do usuário (alternativa ao id, como no A2A).",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 200 },
            Example = ExemploUsuarioNome,
        },
        new()
        {
            Name = LeitorIdentificacaoHttp.CabecalhoChamador,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Id do chamador.",
            Schema = EsquemaIdentificadorOpenApi(),
            Example = ExemploChamadorId,
        },
    ];

    private static OpenApiSchema EsquemaIdentificadorOpenApi() => new()
    {
        Type = JsonSchemaType.String,
        MaxLength = IdentificacaoSolicitante.TamanhoMaximo,
        Pattern = @"^[A-Za-z0-9._:@-]+$",
    };

    private static OpenApiRequestBody CorpoMultipartExecucaoOpenApi() => new()
    {
        Required = true,
        Description = "Pergunta e/ou XMLs de NF-e (ao menos um).",
        Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
        {
            ["multipart/form-data"] = new()
            {
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                    {
                        ["pergunta"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            MaxLength = 4000,
                        },
                        ["arquivos"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                        },
                        ["sessaoId"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Format = "uuid",
                        },
                        ["correlacaoId"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            MaxLength = 128,
                        },
                    },
                },
                Encoding = new Dictionary<string, OpenApiEncoding>(StringComparer.Ordinal)
                {
                    ["arquivos"] = new OpenApiEncoding
                    {
                        ContentType = "application/xml, text/xml",
                    },
                },
            },
        },
    };
}
