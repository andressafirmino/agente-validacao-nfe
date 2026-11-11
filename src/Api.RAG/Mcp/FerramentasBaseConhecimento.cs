using System.ComponentModel;
using Api.RAG.Servicos;
using ModelContextProtocol.Server;

namespace Api.RAG.Mcp;

[McpServerToolType]
public sealed class FerramentasBaseConhecimento(CatalogoTrechos catalogo)
{
    [McpServerTool(Name = ConsultaManuaisMcp.NomeFerramenta), Description(
        "Busca trechos de manuais oficiais e notas técnicas vigentes na data de referência da NF-e.")]
    public RespostaBusca BuscarTrechosManual(
        [Description("Tipo do documento fiscal; use NF-e.")] string tipoDocumento,
        [Description("Modelo do documento; use 55.")] string modelo,
        [Description("Versão do layout; use 4.00.")] string versaoLayout,
        [Description("Data de referência do documento (yyyy-MM-dd).")] string dataReferencia,
        [Description("Texto de consulta montado a partir dos achados; não incluir XML.")] string consulta,
        [Description("Ids de regras do catálogo relacionadas, se houver.")] string[]? regrasRelacionadas,
        [Description("Quantidade máxima de trechos (1 a 20).")] int quantidadeMaxima) =>
        catalogo.Buscar(new ParametrosBusca(
            tipoDocumento,
            modelo,
            versaoLayout,
            dataReferencia,
            consulta,
            regrasRelacionadas,
            quantidadeMaxima));
}

/// <summary>Nome compartilhado com o cliente em AuditoriaNFe.Infrastructure.</summary>
internal static class ConsultaManuaisMcp
{
    public const string NomeFerramenta = "buscar_trechos_manual";
}
