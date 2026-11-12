using Api.Agente.Hospedagem;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Agente.Documentacao;

/// <summary>OpenAPI e Swagger UI em <c>Development</c> (research R-20) e no modo <c>Homologacao</c> (FR-052g).</summary>
public static class DocumentacaoApiExtensions
{
    public const string RotaOpenApi = "/openapi/v1.json";

    public static IServiceCollection AdicionarDocumentacaoApi(this IServiceCollection services, IHostEnvironment ambiente, ModoHospedagem modo)
    {
        if (Habilitada(ambiente, modo))
        {
            services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Api.Agente",
                    Version = "v1",
                    Description =
                        "Canal direto de auditoria de NF-e. **Identificação (opcional na v1, obrigatória para testar contexto):** "
                        + "informe `X-Usuario-Id` *ou* `X-Usuario-Nome` (mesma lógica do A2A) e o mesmo `X-Chamador-Id` em "
                        + "`POST /execucoes`, `GET /contexto-usuario` e demais rotas `/api/v1`. "
                        + "Exemplos: `homolog-01` + `teste-vercel`, ou nome `Maria Silva` + chamador. "
                        + "Multipart de execução: pelo menos pergunta ou XML. SOFIA/A2A não usa estes cabeçalhos.",
                };

                document.Servers =
                [
                    new OpenApiServer
                    {
                        Url = "/",
                        Description = "Mesma origem da barra de endereços (use https:// no Vercel).",
                    },
                ];

                return Task.CompletedTask;
            }));
        }

        return services;
    }

    public static WebApplication UsarDocumentacaoApi(this WebApplication app, ModoHospedagem modo)
    {
        if (Habilitada(app.Environment, modo))
        {
            app.MapOpenApi();
            app.UseSwaggerUI(opcoes =>
            {
                opcoes.SwaggerEndpoint(RotaOpenApi, "Api.Agente v1");
                opcoes.DocumentTitle = "Api.Agente — canal direto";
            });
        }

        return app;
    }

    private static bool Habilitada(IHostEnvironment ambiente, ModoHospedagem modo) =>
        ambiente.IsDevelopment() || modo == ModoHospedagem.Homologacao;
}
