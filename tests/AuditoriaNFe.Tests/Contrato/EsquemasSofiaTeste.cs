using AuditoriaNFe.Tests.Arquitetura;
using Json.Schema;

namespace AuditoriaNFe.Tests.Contrato;

internal static class EsquemasSofiaTeste
{
    private static readonly Lazy<JsonSchema> Solicitacao = new(CarregarSolicitacao);
    private static readonly Lazy<JsonSchema> Resultado = new(CarregarResultado);

    public static JsonSchema SolicitacaoAnalise => Solicitacao.Value;
    public static JsonSchema ResultadoAnalise => Resultado.Value;

    private static string PastaContratos => Path.Combine(
        Repositorio.Raiz(), "specs", "001-agente-auditoria-nfe", "contracts");

    private static JsonSchema CarregarSolicitacao() =>
        JsonSchema.FromFile(Path.Combine(PastaContratos, "sofia-solicitacao-analise-v1.schema.json"));

    private static JsonSchema CarregarResultado() =>
        JsonSchema.FromFile(Path.Combine(PastaContratos, "sofia-resultado-analise-v1.schema.json"));
}
