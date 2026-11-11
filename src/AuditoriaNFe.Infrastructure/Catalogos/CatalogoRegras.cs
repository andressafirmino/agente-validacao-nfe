using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Domain.Regras;

namespace AuditoriaNFe.Infrastructure.Catalogos;

/// <summary>Catálogos carregados por <see cref="CarregadorCatalogos"/> e o aplicador de regras com a versão do catálogo.</summary>
public sealed class CatalogoRegras : ICatalogoRegras
{
    public CatalogoRegras(CatalogosCarregados carregados)
    {
        ArgumentNullException.ThrowIfNull(carregados);
        Carregados = carregados;
        Aplicador = new AplicadorRegras(carregados.Regras, ImplementacoesRegras.Todas(), carregados.VersaoCatalogoRegras);
        CaminhosCatalogoExtracao = carregados.CamposExtracao.Select(c => c.Caminho).ToHashSet(StringComparer.Ordinal);
        CaminhosNumericosExtracao = carregados.CamposExtracao
            .Where(c => string.Equals(c.Tipo, "decimal", StringComparison.Ordinal))
            .Select(c => c.Caminho)
            .ToHashSet(StringComparer.Ordinal);
        ClassePorCaminhoExtracao = carregados.CamposExtracao.ToDictionary(c => c.Caminho, c => c.Classe, StringComparer.Ordinal);
    }

    public CatalogosCarregados Carregados { get; }
    public string VersaoCatalogoRegras => Carregados.VersaoCatalogoRegras;
    public string VersaoCatalogoExtracao => Carregados.VersaoCatalogoExtracao;
    public IReadOnlyList<RegraConteudo> Regras => Carregados.Regras;
    public AplicadorRegras Aplicador { get; }
    public IReadOnlySet<string> CaminhosCatalogoExtracao { get; }
    public IReadOnlySet<string> CaminhosNumericosExtracao { get; }
    public IReadOnlyDictionary<string, string> ClassePorCaminhoExtracao { get; }
}
