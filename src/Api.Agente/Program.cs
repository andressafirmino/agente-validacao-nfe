using Api.Agente.Documentacao;
using Api.Agente.Endpoints;
using Api.Agente.Erros;
using Api.Agente.Hospedagem;
using Api.Agente.Limites;
using Api.Agente.Observabilidade;
using Api.Agente.Seguranca;
using AuditoriaNFe.Application;
using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Application.Configuracao;
using AuditoriaNFe.Application.Contratos;
using AuditoriaNFe.Infrastructure;
using AuditoriaNFe.Infrastructure.Persistencia.Postgres;
using AuditoriaNFe.Infrastructure.Saude;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

GuardaDesvioD05.Verificar(builder.Environment, builder.Configuration);

if (HospedagemOpcoes.LerModo(builder.Configuration) == ModoHospedagem.SomenteAgentCard)
{
    builder.Services.AdicionarSomenteAgentCard(builder.Configuration);
    var appCard = builder.Build();
    appCard.MapearSomenteAgentCard();
    await appCard.RunAsync();
    return;
}

builder.Services
    .AdicionarOpcoesAuditoria(builder.Configuration)
    .AdicionarComum()
    .AdicionarPersistenciaMongo(builder.Configuration)
    .AdicionarPersistenciaConsumo(builder.Configuration)
    .AdicionarIa(builder.Environment.EnvironmentName)
    .AdicionarProcessamentoNFe()
    .AdicionarFluxoAuditoria()
    .AdicionarObservabilidade(builder.Configuration)
    .AdicionarLimitesHttp()
    .AdicionarCorsNavegador(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(opcoes => OpcoesJsonContrato.Aplicar(opcoes.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorExcecoes>();

const string Prontidao = "ready";
builder.Services.AddHealthChecks()
    .AddCheck<VerificacaoMongo>(VerificacaoMongo.Nome, tags: [Prontidao])
    .AddCheck<VerificacaoPostgres>(VerificacaoPostgres.Nome, tags: [Prontidao])
    .AddCheck<VerificacaoDiretorioAprovacao>(VerificacaoDiretorioAprovacao.Nome, tags: [Prontidao])
    .AddCheck<SaudeSchemasCatalogos>(SaudeSchemasCatalogos.Nome, tags: [Prontidao]);

builder.Services.AdicionarDocumentacaoApi(builder.Environment);

var app = builder.Build();

app.Services.GetRequiredService<IProcessadorXml>();
app.Services.GetRequiredService<ICatalogoRegras>();
await MigracoesConsumo.AplicarSeHabilitadoAsync(app.Services);

app.UseExceptionHandler();
app.UsarDocumentacaoApi();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = registro => registro.Tags.Contains(Prontidao) });
app.MapearApiV1().MapearExecucoes().MapearSessoes().MapearContextoUsuario();

await app.RunAsync();

public partial class Program;
