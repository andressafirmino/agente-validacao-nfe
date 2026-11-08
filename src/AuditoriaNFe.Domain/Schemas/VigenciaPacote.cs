namespace AuditoriaNFe.Domain.Schemas;

/// <summary>
/// Período de produção de um pacote de schema (data-model §5, FR-010b, FR-010e). <see cref="Fim"/>
/// nulo só no último pacote, aberto até a data atual.
/// </summary>
public sealed record VigenciaPacote(int Ordem, string Id, DateOnly Inicio, DateOnly? Fim, DateOnly? FimConvivencia = null);
