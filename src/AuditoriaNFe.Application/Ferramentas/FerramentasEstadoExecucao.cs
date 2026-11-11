using AuditoriaNFe.Domain.Calculos;
using AuditoriaNFe.Domain.Documentos;
using AuditoriaNFe.Domain.Validacao;

namespace AuditoriaNFe.Application.Ferramentas;

/// <summary>Dados da sessão e da execução expostos somente às ferramentas de leitura.</summary>
public sealed class FerramentasEstadoExecucao
{
    public IReadOnlyList<DocumentoFerramenta> Documentos { get; init; } = [];
    public IReadOnlyList<Achado> Achados { get; init; } = [];
    public IReadOnlyList<RegistroCalculo> Calculos { get; init; } = [];

    public void RegistrarCalculo(RegistroCalculo registro, List<RegistroCalculo> destino)
    {
        ArgumentNullException.ThrowIfNull(registro);
        ArgumentNullException.ThrowIfNull(destino);
        destino.Add(registro);
    }
}

/// <summary>Visão de documento para as ferramentas <c>listar_documentos</c> e cálculo.</summary>
public sealed record DocumentoFerramenta(
    string ChaveAcesso,
    string Arquivo,
    DateOnly? DataReferencia,
    string? OrigemDataReferencia,
    string? PacoteAplicado,
    string ValidacaoLayout,
    string? MotivoValidacaoLayout,
    string ValidacaoConteudo,
    string? MotivoValidacaoConteudo,
    string? DuplicadoDe,
    MotivoExclusaoCalculo? SituacaoCalculo,
    IReadOnlyList<CampoExtraido> Campos);
