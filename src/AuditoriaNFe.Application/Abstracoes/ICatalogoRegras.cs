using AuditoriaNFe.Domain.Regras;

namespace AuditoriaNFe.Application.Abstracoes;

/// <summary>Catálogos de regras e de extração carregados e validados na inicialização (data-model §4 e §6).</summary>
public interface ICatalogoRegras
{
    string VersaoCatalogoRegras { get; }
    string VersaoCatalogoExtracao { get; }
    IReadOnlyList<RegraConteudo> Regras { get; }
    AplicadorRegras Aplicador { get; }
}
