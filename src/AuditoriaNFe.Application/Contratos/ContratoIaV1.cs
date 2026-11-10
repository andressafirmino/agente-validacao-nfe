using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AuditoriaNFe.Application.Contratos;

/// <summary>
/// Saída estruturada da etapa 7 (data-model §15). O schema gerado pelo tipo com
/// <see cref="OpcoesJson"/> é o mesmo de contracts/contrato-ia-v1.schema.json (R-05). Sem ids,
/// datas, consumo, resultado geral nem contexto do usuário (P09, FR-057).
/// </summary>
public sealed record ContratoIaV1(
    [property: AllowedValues(ContratoIa.Versao)] string VersaoContrato,
    IReadOnlyList<IndicioIaV1> Indicios,
    IReadOnlyList<AusenciaEvidenciaIaV1> AusenciasEvidencia,
    RespostaIaV1? Resposta,
    bool ForaDeEscopo,
    bool InformacaoIndisponivel,
    IReadOnlyList<RecomendacaoIaV1> Recomendacoes,
    [property: MinLength(1)] string Resumo)
{
    /// <summary>
    /// Opções usadas na geração do schema e na desserialização da resposta do provedor: camelCase,
    /// todas as propriedades obrigatórias e nenhuma propriedade fora do contrato.
    /// </summary>
    public static JsonSerializerOptions OpcoesJson { get; } = CriarOpcoes();

    private static JsonSerializerOptions CriarOpcoes()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            PropertyNameCaseInsensitive = false,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        opcoes.MakeReadOnly();
        return opcoes;
    }
}

public sealed record IndicioIaV1(
    [property: MinLength(1)] string Descricao,
    [property: AllowedValues("Alta", "Media", "Baixa", "Informativa")] string Severidade,
    [property: MinLength(1)] IReadOnlyList<EvidenciaIaV1> Evidencias,
    IReadOnlyList<string> AchadosRelacionados);

/// <summary><c>Tipo</c>: <c>CampoXml</c>, <c>RegraCatalogo</c>, <c>TrechoManual</c> ou <c>Calculo</c>.</summary>
public sealed record EvidenciaIaV1(
    [property: AllowedValues("CampoXml", "RegraCatalogo", "TrechoManual", "Calculo")] string Tipo,
    string? ChaveAcesso,
    string? Caminho,
    string? Valor,
    string? RegraId,
    string? ChunkId,
    string? CalculoId);

public sealed record AusenciaEvidenciaIaV1([property: MinLength(1)] string PontoAvaliado, [property: MinLength(1)] string Motivo);

public sealed record RespostaIaV1(
    [property: MinLength(1)] string Texto,
    IReadOnlyList<ValorCitadoIaV1> ValoresCitados,
    IReadOnlyList<DocumentoExcluidoIaV1> DocumentosExcluidos);

public sealed record ValorCitadoIaV1(
    [property: RegularExpression(@"^-?[0-9]+(\.[0-9]+)?$")] string Valor,
    string? CalculoId,
    string? ChaveAcesso,
    string? Caminho);

public sealed record DocumentoExcluidoIaV1(string ChaveAcesso, string Motivo);

public sealed record RecomendacaoIaV1(
    [property: MinLength(1)] string Texto,
    [property: MinLength(1)] IReadOnlyList<string> AchadosVinculados,
    IReadOnlyList<ValorCitadoIaV1> ValoresCitados);
