using AuditoriaNFe.Domain.Sessoes;

namespace AuditoriaNFe.Application.Ferramentas;

/// <summary>Lista permitida por tipo de execução (contracts/ferramentas-agente.md §3).</summary>
public static class PoliticaFerramentas
{
    public const string ListarDocumentos = "listar_documentos";
    public const string ObterCamposExtraidos = "obter_campos_extraidos";
    public const string CalcularTotal = "calcular_total";
    public const string ListarAchados = "listar_achados";

    private static readonly IReadOnlyList<string> LeituraLote = [ListarDocumentos, ListarAchados];
    private static readonly IReadOnlyList<string> Perguntas = [ListarDocumentos, ObterCamposExtraidos, CalcularTotal, ListarAchados];

    public static IReadOnlyList<string> Permitidas(TipoExecucao tipo) => tipo switch
    {
        TipoExecucao.AnaliseLote => LeituraLote,
        TipoExecucao.Pergunta or TipoExecucao.AnaliseComPergunta => Perguntas,
        _ => [],
    };

    public static bool Autorizada(TipoExecucao tipo, string nome) =>
        Permitidas(tipo).Contains(nome, StringComparer.Ordinal);
}
