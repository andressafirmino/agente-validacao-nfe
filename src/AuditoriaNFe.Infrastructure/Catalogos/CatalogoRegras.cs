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
    }

    public CatalogosCarregados Carregados { get; }
    public string VersaoCatalogoRegras => Carregados.VersaoCatalogoRegras;
    public string VersaoCatalogoExtracao => Carregados.VersaoCatalogoExtracao;
    public IReadOnlyList<RegraConteudo> Regras => Carregados.Regras;
    public AplicadorRegras Aplicador { get; }
}
