using Api.RAG;
using Api.RAG.Infraestrutura;
using Api.RAG.Seguranca;

var builder = WebApplication.CreateBuilder(args);
ApiRagHosting.ConfigurarServicos(builder);
var app = builder.Build();
app.UsarProtecaoAdministracaoRag();
ApiRagHosting.MapearEndpoints(app);
await MigracoesRag.AplicarSeHabilitadoAsync(app.Services);
app.Run();

public partial class Program;
