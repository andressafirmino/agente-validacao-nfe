using System.ComponentModel.DataAnnotations;

namespace AuditoriaNFe.Application.Configuracao;

/// <summary>Consulta a manuais oficiais por MCP (FR-047a; feature 002).</summary>
public sealed class McpOpcoes
{
    public const string Secao = "Mcp";

    public bool Habilitado { get; set; }

    /// <summary>Etapa 6: consulta de capacidades externas no mesmo servidor MCP (FR-047a).</summary>
    public bool FerramentasExternasHabilitado { get; set; }

    /// <summary>URL absoluta do servidor MCP; obrigatória quando <see cref="Habilitado"/>.</summary>
    public string? Endpoint { get; set; }

    [Range(typeof(TimeSpan), "00:00:01", "00:02:00", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    [Range(1, 50)]
    public int QuantidadeMaximaTrechos { get; set; } = 5;
}
