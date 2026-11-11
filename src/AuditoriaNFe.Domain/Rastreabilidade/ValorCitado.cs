namespace AuditoriaNFe.Domain.Rastreabilidade;

/// <summary>Valor financeiro declarado pela IA para validação semântica (FR-027).</summary>
public sealed record ValorCitado(string Valor, Guid? CalculoId, string? ChaveAcesso, string? Caminho);
