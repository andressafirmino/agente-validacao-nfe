using AuditoriaNFe.Domain.Regras;
using AuditoriaNFe.Infrastructure.Comum;

namespace AuditoriaNFe.Infrastructure.Catalogos;

/// <summary>
/// Carrega e valida o catálogo de extração e o de regras (data-model §4 e §6). Regra sem fonte, sem
/// vigência, com implementação inexistente, com campo fora do catálogo de extração ou com versões
/// sobrepostas falha com <see cref="CargaInvalidaException"/>.
/// </summary>
/// <remarks>
/// Extração: <c>{ versaoCatalogo, campos: [{ caminho, tipo, classe }] }</c>, com <c>det[*]</c> nos
/// campos dos itens. Regras: <c>{ versaoCatalogo, aprovacao: { por, em }, fontes: [{ documento,
/// versao, sha256 }], regras: [{ id, versaoRegra, grupo, descricao, fonte: { documento, versao,
/// paginas }, vigencia: { inicio, fim }, severidade, tolerancia, aplicacaoSefaz: { tipo,
/// codigoRejeicao }, condicoes, implementacao }] }</c>.
/// </remarks>
public static class CarregadorCatalogos
{
    public static CatalogosCarregados Carregar(
        string caminhoRegras,
        string caminhoExtracao,
        IEnumerable<IImplementacaoRegra> implementacoes) => throw new NotImplementedException("T076");
}

public sealed record CatalogosCarregados(
    string VersaoCatalogoRegras,
    IReadOnlyList<RegraConteudo> Regras,
    string VersaoCatalogoExtracao,
    IReadOnlyList<CampoCatalogoExtracao> CamposExtracao);

public sealed record CampoCatalogoExtracao(string Caminho, string Tipo, string Classe);
