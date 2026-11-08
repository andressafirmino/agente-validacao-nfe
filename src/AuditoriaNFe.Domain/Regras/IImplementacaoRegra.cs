namespace AuditoriaNFe.Domain.Regras;

/// <summary>Implementação determinística de uma versão de regra do catálogo (data-model §6, <c>implementacao</c>).</summary>
public interface IImplementacaoRegra
{
    string Id { get; }

    /// <summary>Campos lidos, com <c>det[*]</c> para os itens; todos DEVEM estar no catálogo de extração.</summary>
    IReadOnlyList<string> Campos { get; }

    AvaliacaoRegra Avaliar(DadosDocumento documento, RegraConteudo regra);
}
