namespace AuditoriaNFe.Domain.Parecer;

/// <summary>Textos fixos de limitação do parecer (data-model §10).</summary>
public static class LimitacoesParecer
{
    public const string ConsultaManuaisNaoHabilitada = "consulta a manuais oficiais não habilitada";
    public const string UsuarioNaoIdentificado = "usuário não identificado";
    public const string SemBaseAnalisada = "não há documentos analisados nesta sessão para responder à pergunta";
}
