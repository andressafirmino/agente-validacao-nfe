using System.Text.Json;

namespace AuditoriaNFe.Application.Contratos;

/// <summary>
/// Saída estruturada da etapa 7 (data-model §15). O schema gerado pelo tipo com
/// <see cref="OpcoesJson"/> é o mesmo de contracts/contrato-ia-v1.schema.json (R-05). Sem ids,
/// datas, consumo, resultado geral nem contexto do usuário (P09, FR-057).
/// </summary>
public sealed record ContratoIaV1(
    string VersaoContrato,
    IReadOnlyList<IndicioIaV1> Indicios,
    IReadOnlyList<AusenciaEvidenciaIaV1> AusenciasEvidencia,
    RespostaIaV1? Resposta,
    bool ForaDeEscopo,
    bool InformacaoIndisponivel,
    IReadOnlyList<RecomendacaoIaV1> Recomendacoes,
    string Resumo)
{
    /// <summary>Opções usadas na geração do schema e na desserialização da resposta do provedor.</summary>
    public static JsonSerializerOptions OpcoesJson => throw new NotImplementedException("T080");
}

public sealed record IndicioIaV1(
    string Descricao,
    string Severidade,
    IReadOnlyList<EvidenciaIaV1> Evidencias,
    IReadOnlyList<string> AchadosRelacionados);

/// <summary><c>Tipo</c>: <c>CampoXml</c>, <c>RegraCatalogo</c>, <c>TrechoManual</c> ou <c>Calculo</c>.</summary>
public sealed record EvidenciaIaV1(
    string Tipo,
    string? ChaveAcesso,
    string? Caminho,
    string? Valor,
    string? RegraId,
    string? ChunkId,
    string? CalculoId);

public sealed record AusenciaEvidenciaIaV1(string PontoAvaliado, string Motivo);

public sealed record RespostaIaV1(
    string Texto,
    IReadOnlyList<ValorCitadoIaV1> ValoresCitados,
    IReadOnlyList<DocumentoExcluidoIaV1> DocumentosExcluidos);

public sealed record ValorCitadoIaV1(string Valor, string? CalculoId, string? ChaveAcesso, string? Caminho);

public sealed record DocumentoExcluidoIaV1(string ChaveAcesso, string Motivo);

public sealed record RecomendacaoIaV1(
    string Texto,
    IReadOnlyList<string> AchadosVinculados,
    IReadOnlyList<ValorCitadoIaV1> ValoresCitados);
