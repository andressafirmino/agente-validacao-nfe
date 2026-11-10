namespace AuditoriaNFe.Infrastructure.Comum;

/// <summary>Arquivo versionado (manifesto de schemas ou catálogo) inválido; impede a inicialização.</summary>
public sealed class CargaInvalidaException(string mensagem) : Exception(mensagem);
