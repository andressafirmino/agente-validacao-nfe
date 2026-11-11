namespace Sofia.Simulator.Cenarios;

public interface ICenarioSimulador
{
    string Nome { get; }

    Task ExecutarAsync(ContextoCenario contexto, CancellationToken cancellationToken);
}

public sealed class ContextoCenario(
    A2A.V0_3.AgentCard card,
    A2A.V0_3.A2AClient clienteA2A,
    TextWriter saida)
{
    public A2A.V0_3.AgentCard Card { get; } = card;
    public A2A.V0_3.A2AClient ClienteA2A { get; } = clienteA2A;
    public TextWriter Saida { get; } = saida;
}
