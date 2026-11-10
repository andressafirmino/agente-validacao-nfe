namespace Api.Agente.Hospedagem;

/// <summary>Modo de hospedagem do host (FR-052f, FR-052g; desvios D-08 e D-09; ADR-0012 e ADR-0013).</summary>
public enum ModoHospedagem
{
    Completo,

    /// <summary>Só o Agent Card e a vivacidade; aceito fora de <c>Development</c> (D-08).</summary>
    SomenteAgentCard,

    /// <summary>
    /// Agente completo, sem autenticação, para a homologação pública com dados sintéticos; aceito fora
    /// de <c>Development</c> (D-09).
    /// </summary>
    Homologacao,
}

public sealed class HospedagemOpcoes
{
    public const string Secao = "Hospedagem";
    public const string ChaveModo = $"{Secao}:{nameof(Modo)}";

    public ModoHospedagem Modo { get; set; } = ModoHospedagem.Completo;

    /// <summary>
    /// Lido antes de montar o host, porque decide o que é registrado. Só aceita os nomes do enum:
    /// um número fora da lista seria convertido pelo binder sem erro.
    /// </summary>
    public static ModoHospedagem LerModo(IConfiguration configuration)
    {
        var valor = configuration[ChaveModo]?.Trim();
        if (string.IsNullOrEmpty(valor))
        {
            return ModoHospedagem.Completo;
        }

        var nome = Enum.GetNames<ModoHospedagem>().FirstOrDefault(n => string.Equals(n, valor, StringComparison.OrdinalIgnoreCase));
        return nome is not null
            ? Enum.Parse<ModoHospedagem>(nome)
            : throw new InvalidOperationException(
                $"{ChaveModo} inválido: use {nameof(ModoHospedagem.Completo)}, {nameof(ModoHospedagem.SomenteAgentCard)} ou {nameof(ModoHospedagem.Homologacao)} (desvios D-08 e D-09).");
    }
}
