using A2A.V0_3;

namespace Sofia.Simulator.Descoberta;

/// <summary>Obtém o Agent Card v0.3 (research R-12, FR-051a).</summary>
public sealed class DescobertaAgente(Uri enderecoAgente, HttpClient http)
{
    public async Task<AgentCard> ObterCardAsync(CancellationToken cancellationToken)
    {
        try
        {
            var resolvedor = new A2ACardResolver(enderecoAgente, http);
            return await resolvedor.GetAgentCardAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new DescobertaAgenteException(
                $"Falha de descoberta: não foi possível obter o Agent Card em {enderecoAgente}. {ex.Message}",
                ex);
        }
    }
}

public sealed class DescobertaAgenteException(string mensagem, Exception? interna = null) : Exception(mensagem, interna);
