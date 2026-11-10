namespace AuditoriaNFe.Application.Agente;

/// <summary>Instruções versionadas do agente interno, embutidas no assembly (P07, FR-053).</summary>
public static class InstrucoesAgente
{
    public const string Versao = "v2";

    private const string Recurso = "AuditoriaNFe.Application.Agente.Instrucoes.instrucoes-agente.v2.md";

    private static readonly Lazy<string> Conteudo = new(Carregar);

    public static string Texto => Conteudo.Value;

    private static string Carregar()
    {
        using var fluxo = typeof(InstrucoesAgente).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"Recurso {Recurso} não encontrado.");
        using var leitor = new StreamReader(fluxo);
        return leitor.ReadToEnd();
    }
}
