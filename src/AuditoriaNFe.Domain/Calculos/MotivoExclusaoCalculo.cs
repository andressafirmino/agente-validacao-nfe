namespace AuditoriaNFe.Domain.Calculos;

/// <summary>Motivos do contrato de ferramentas §2.3 e FR-031.</summary>
public enum MotivoExclusaoCalculo
{
    ErroEstrutural,
    Duplicado,
    NaoSuportado,
    ForaDoFiltro,
}
