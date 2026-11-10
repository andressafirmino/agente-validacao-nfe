namespace AuditoriaNFe.Application.Abstracoes;

/// <summary>Provedor de IA indisponível, com erro ou sem resposta em <c>Ia:TimeoutChamada</c> (ERR-014).</summary>
public sealed class ProvedorIaIndisponivelException(string mensagem, Exception? interna = null)
    : Exception(mensagem, interna);
