namespace AuditoriaNFe.Application.ContextProviders;

/// <summary>
/// Tudo o que o agente interno recebe numa execução: o contexto da execução, o resumo da base da
/// sessão e, para usuário identificado com contexto, o contexto do usuário.
/// </summary>
public sealed record ContextoAgenteIa(ContextoExecucaoIa Execucao, ContextoSessaoIa Sessao, ContextoUsuarioIa? Usuario = null)
{
    /// <summary>Documentos que a IA pode citar em evidências <c>CampoXml</c>: os da execução e os da sessão.</summary>
    public IEnumerable<DocumentoContextoIa> Documentos => Execucao.Documentos.Concat(Sessao.DocumentosAnteriores);

    /// <summary>Regras que a IA pode citar em evidências <c>RegraCatalogo</c>.</summary>
    public IEnumerable<RegraContextoIa> Regras => Execucao.Regras.Concat(Sessao.RegrasAnteriores);
}
