using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Domain.Regras;

/// <summary>
/// Aplica a um documento a versão de cada regra vigente na data de referência (FR-013a). Regra sem
/// versão vigente não é avaliada nem registrada. Cada divergência vira achado
/// <see cref="CategoriaAchado.InconsistenciaNegocio"/> com a severidade do catálogo (FR-014, FR-023).
/// </summary>
public sealed class AplicadorRegras
{
    public AplicadorRegras(IReadOnlyList<RegraConteudo> regras, IEnumerable<IImplementacaoRegra> implementacoes) =>
        throw new NotImplementedException("T063");

    public ResultadoAplicacaoRegras Aplicar(DadosDocumento documento, Func<Guid> gerarId) =>
        throw new NotImplementedException("T063");
}

public sealed record ResultadoAplicacaoRegras(IReadOnlyList<Achado> Achados, IReadOnlyList<RegraAplicada> RegrasAplicadas);
