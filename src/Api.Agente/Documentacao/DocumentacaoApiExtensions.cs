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
                        "Canal direto `/api/v1`. Cabeçalhos: X-Usuario-Id (ou X-Usuario-Nome) e X-Chamador-Id — use o mesmo par em todas as chamadas.",
                };

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
