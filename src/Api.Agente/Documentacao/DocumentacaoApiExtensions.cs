using Api.Agente.Hospedagem;

namespace Api.Agente.Documentacao;

/// <summary>OpenAPI e Swagger UI em <c>Development</c> (research R-20) e no modo <c>Homologacao</c> (FR-052g).</summary>
public static class DocumentacaoApiExtensions
{
    public const string RotaOpenApi = "/openapi/v1.json";

    public static IServiceCollection AdicionarDocumentacaoApi(this IServiceCollection services, IHostEnvironment ambiente, ModoHospedagem modo)
    {
        if (Habilitada(ambiente, modo))
        {
            services.AddOpenApi();
        }

        return services;
    }

    public static WebApplication UsarDocumentacaoApi(this WebApplication app, ModoHospedagem modo)
    {
        if (Habilitada(app.Environment, modo))
        {
            app.MapOpenApi();
            app.UseSwaggerUI(opcoes => opcoes.SwaggerEndpoint(RotaOpenApi, "Api.Agente v1"));
        }

        return app;
    }

    private static bool Habilitada(IHostEnvironment ambiente, ModoHospedagem modo) =>
        ambiente.IsDevelopment() || modo == ModoHospedagem.Homologacao;
}
