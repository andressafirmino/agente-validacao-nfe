namespace AuditoriaNFe.Domain.Regras;

/// <summary>Como a SEFAZ aplica a regra na autorização e o código de rejeição correspondente.</summary>
public sealed record AplicacaoSefaz(TipoAplicacaoSefaz Tipo, string CodigoRejeicao);

public enum TipoAplicacaoSefaz
{
    Obrigatoria,
    Facultativa,
}
