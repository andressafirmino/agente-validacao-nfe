using A2A.V0_3;
using Sofia.Simulator.Cenarios;
using Sofia.Simulator.Descoberta;
using Sofia.Simulator.Exibicao;

namespace Sofia.Simulator;

/// <summary>Orquestra descoberta e cenários (testável com <see cref="HttpClient"/> injetado).</summary>
public static class ExecutorSimulador
{
    private static readonly IReadOnlyDictionary<string, ICenarioSimulador> Cenarios =
        new ICenarioSimulador[] { new CenarioBasico(), new CenarioDocumentoNaoSuportado(), new CenarioContratoInvalido() }
            .ToDictionary(c => c.Nome, StringComparer.OrdinalIgnoreCase);

    public static async Task<int> ExecutarAsync(
        Uri enderecoAgente,
        string nomeCenario,
        HttpClient http,
        TextWriter saida,
        TextWriter erro,
        CancellationToken cancellationToken = default)
    {
        if (!Cenarios.TryGetValue(nomeCenario, out var cenario))
        {
            await erro.WriteLineAsync($"Cenário desconhecido: {nomeCenario}. Use: {string.Join(", ", Cenarios.Keys)}.");
            return 2;
        }

        AgentCard card;
        try
        {
            var descoberta = new DescobertaAgente(enderecoAgente, http);
            card = await descoberta.ObterCardAsync(cancellationToken);
        }
        catch (DescobertaAgenteException ex)
        {
            await erro.WriteLineAsync(ex.Message);
            return 1;
        }

        ExibicaoConsole.ExibirCard(saida, card);

        var clienteA2A = new A2AClient(new Uri(card.Url), http);
        var contexto = new ContextoCenario(card, clienteA2A, saida);
        await cenario.ExecutarAsync(contexto, cancellationToken);
        return 0;
    }
}
