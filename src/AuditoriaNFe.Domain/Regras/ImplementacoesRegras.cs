using AuditoriaNFe.Domain.Regras.CobrancaPagamento;
using AuditoriaNFe.Domain.Regras.ICMSTot;
using AuditoriaNFe.Domain.Regras.IbsCbsIs;
using AuditoriaNFe.Domain.Regras.ISSQNtot;

namespace AuditoriaNFe.Domain.Regras;

/// <summary>Todas as implementações de regra do Domínio, uma por regra e versão do catálogo aprovado.</summary>
public static class ImplementacoesRegras
{
    private static readonly IReadOnlyList<IImplementacaoRegra> Implementacoes =
    [
        .. RegrasICMSTot.Todas,
        .. RegrasISSQNtot.Todas,
        .. RegrasIbsCbsIs.Todas,
        .. RegrasCobrancaPagamento.Todas,
    ];

    public static IReadOnlyList<IImplementacaoRegra> Todas() => Implementacoes;
}
