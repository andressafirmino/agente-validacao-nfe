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
            Description =
                "Identificador opaco do usuário no canal direto (até 128 caracteres `[A-Za-z0-9._:@-]`). "
                + "Preencha este cabeçalho **ou** `X-Usuario-Nome` (como no A2A/SOFIA) para `usuarioIdentificado` "
                + "e para gravar/consultar o contexto. Ausente sem nome → `anonimo`.",
            Schema = EsquemaIdentificadorOpenApi(),
            Example = ExemploUsuarioId,
        },
        new()
        {
            Name = LeitorIdentificacaoHttp.CabecalhoUsuarioNome,
            In = ParameterLocation.Header,
            Required = false,
            Description =
                "Nome do usuário (como em SOFIA/A2A): gera o mesmo `usuarioId` interno (`nome-sha256:…`). "
                + "Use quando não enviar `X-Usuario-Id`. Requer o mesmo `X-Chamador-Id` em todas as chamadas "
                + "da sessão e do contexto.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 200 },
            Example = ExemploUsuarioNome,
        },
        new()
        {
            Name = LeitorIdentificacaoHttp.CabecalhoChamador,
            In = ParameterLocation.Header,
            Required = false,
            Description =
                "Sistema chamador (não verificado na v1). Use o mesmo valor em execução, contexto e sessão. "
                + "Ausente → `nao-informado`.",
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
        Description =
            "Envie **pelo menos** `pergunta` **ou** um ou mais `arquivos` XML (ERR-001). "
            + "Para homologar contexto, informe os cabeçalhos de identificação acima antes de executar.",
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
                            Description = "Pergunta em linguagem natural (opcional se houver arquivos).",
                        },
                        ["arquivos"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Description = "XMLs de NF-e modelo 55 (opcional se houver pergunta).",
                            Items = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                        },
                        ["sessaoId"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Format = "uuid",
                            Description = "Continuidade de sessão (FR-036).",
                        },
                        ["correlacaoId"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            MaxLength = 128,
                            Description = "Correlação livre do chamador.",
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
