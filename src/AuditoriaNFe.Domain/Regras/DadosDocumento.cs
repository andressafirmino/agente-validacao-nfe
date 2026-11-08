using AuditoriaNFe.Domain.Documentos;

namespace AuditoriaNFe.Domain.Regras;

/// <summary>
/// Dados extraídos de um documento, entregues às regras de conteúdo. Os caminhos são relativos a
/// <c>infNFe</c>, sem namespace, com os itens indexados pelo <c>nItem</c>: <c>total/ICMSTot/vNF</c>,
/// <c>det[2]/imposto/ICMS/ICMS00/vICMS</c>, <c>cobr/dup[1]/vDup</c>, <c>pag/detPag[1]/vPag</c>.
/// Campo opcional ausente vale zero nas somas.
/// </summary>
public sealed class DadosDocumento
{
    public DadosDocumento(string arquivo, string chaveAcesso, DateOnly dataReferencia, IReadOnlyList<CampoExtraido> campos)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arquivo);
        ArgumentException.ThrowIfNullOrWhiteSpace(chaveAcesso);
        ArgumentNullException.ThrowIfNull(campos);
        Arquivo = arquivo;
        ChaveAcesso = chaveAcesso;
        DataReferencia = dataReferencia;
        Campos = campos.ToList().AsReadOnly();
    }

    public string Arquivo { get; }
    public string ChaveAcesso { get; }
    public DateOnly DataReferencia { get; }
    public IReadOnlyList<CampoExtraido> Campos { get; }
}
