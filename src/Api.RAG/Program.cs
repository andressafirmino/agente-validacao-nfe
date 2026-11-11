using Api.RAG;
using Api.RAG.Infraestrutura;

var builder = WebApplication.CreateBuilder(args);
ApiRagHosting.ConfigurarServicos(builder);
var app = builder.Build();
ApiRagHosting.MapearEndpoints(app);
await MigracoesRag.AplicarSeHabilitadoAsync(app.Services);
app.Run();

public partial class Program;
