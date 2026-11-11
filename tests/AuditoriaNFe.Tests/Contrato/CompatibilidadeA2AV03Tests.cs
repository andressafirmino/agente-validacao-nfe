using Api.Agente.A2A;
using Api.Agente.A2A.Sofia;
using A2A.V0_3Compat;
using global::A2A;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AuditoriaNFe.Tests.Contrato;

/// <summary>Spike R-11: card v0.3 e Task com Artifact na versão fixada do SDK A2A.</summary>
public sealed class CompatibilidadeA2AV03Tests
{
    private sealed class ManipuladorSpike : IAgentHandler
    {
        public async Task ExecuteAsync(RequestContext context, AgentEventQueue queue, CancellationToken cancellationToken)
        {
            var updater = new TaskUpdater(queue, context.TaskId, context.ContextId);
            await updater.SubmitAsync(cancellationToken: cancellationToken);
            await updater.StartWorkAsync(context.Message, cancellationToken: cancellationToken);
            if (context.Message.Parts.FirstOrDefault()?.Text?.Contains("falhar", StringComparison.Ordinal) == true)
            {
                await updater.FailAsync(new Message { Parts = [Part.FromText("ERR-007: teste")] }, cancellationToken: cancellationToken);
                return;
            }

            await updater.AddArtifactAsync([Part.FromText("{\"ok\":true}")], name: ContratoSofiaV1.NomeArtifact, cancellationToken: cancellationToken);
            await updater.CompleteAsync(cancellationToken: cancellationToken);
        }

        public Task CancelAsync(RequestContext context, AgentEventQueue queue, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public async Task Card_v03_e_message_send_com_Artifact()
    {
        await using var app = await CriarAppAsync();
        var token = TestContext.Current.CancellationToken;
        using var cliente = app.GetTestClient();

        using var cardResp = await cliente.GetAsync(FabricaAgentCard.Rota, token);
        var cardJson = await cardResp.Content.ReadAsStringAsync(token);
        Assert.Contains("\"protocolVersion\":\"0.3.0\"", cardJson, StringComparison.Ordinal);
        Assert.Contains("\"url\"", cardJson, StringComparison.Ordinal);

        using var rpcResp = await cliente.PostAsync(
            FabricaAgentCard.RotaA2A,
            new StringContent(
                """
                {"jsonrpc":"2.0","id":1,"method":"message/send","params":{"message":{"role":"user","parts":[{"kind":"text","text":"{\"ping\":true}"}]}}}
                """,
                System.Text.Encoding.UTF8,
                "application/json"),
            token);
        var corpo = await rpcResp.Content.ReadAsStringAsync(token);
        Assert.Contains("completed", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ContratoSofiaV1.NomeArtifact, corpo, StringComparison.Ordinal);
    }

    private static async Task<WebApplication> CriarAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ITaskStore, InMemoryTaskStore>();
        builder.Services.AddSingleton<ChannelEventNotifier>();
        builder.Services.AddSingleton<IAgentHandler, ManipuladorSpike>();
        builder.Services.AddSingleton<IA2ARequestHandler>(sp => new A2AServer(
            sp.GetRequiredService<IAgentHandler>(),
            sp.GetRequiredService<ITaskStore>(),
            sp.GetRequiredService<ChannelEventNotifier>(),
            sp.GetRequiredService<ILogger<A2AServer>>(),
            new A2AServerOptions { SupportsStreaming = false }));

        var app = builder.Build();
        var handler = app.Services.GetRequiredService<IA2ARequestHandler>();
        app.MapA2AWithV03Compat(handler, FabricaAgentCard.RotaA2A);
        app.MapAgentCardGetWithV03Compat(
            handler,
            () => Task.FromResult(new AgentCard
            {
                Name = "Spike",
                SupportedInterfaces = [new AgentInterface { Url = "http://localhost/a2a", ProtocolBinding = ProtocolBindingNames.JsonRpc }],
            }),
            FabricaAgentCard.Rota,
            blendedCard: false);

        await app.StartAsync();
        return app;
    }
}
