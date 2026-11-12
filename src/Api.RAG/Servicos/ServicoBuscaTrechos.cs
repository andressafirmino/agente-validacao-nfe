using System.Globalization;
using Api.RAG.Infraestrutura;
using Api.RAG.Infraestrutura.Embeddings;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Api.RAG.Servicos;

/// <summary>Recuperação vetorial com filtro de vigência e boost de regras (research R-05).</summary>
public sealed class ServicoBuscaTrechos(
    IDbContextFactory<RagDbContext> fabrica,
    IProvedorEmbeddings embeddings)
{
    private const double BoostPorRegra = 0.2;

    public async Task<RespostaBusca> BuscarAsync(ParametrosBusca parametros, CancellationToken cancellationToken = default)
    {
        var dataReferencia = DateOnly.ParseExact(parametros.DataReferencia, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var limite = Math.Clamp(parametros.QuantidadeMaxima, 1, 20);
        var consulta = parametros.Consulta?.Trim() ?? string.Empty;
        if (consulta.Length == 0)
        {
            return new RespostaBusca("0.1", []);
        }

        ResultadoEmbedding vetorConsulta;
        try
        {
            vetorConsulta = await embeddings.GerarEmbeddingAsync(consulta, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return new RespostaBusca("0.1", []);
        }

        var embeddingConsulta = new Vector(vetorConsulta.Vetor.ToArray());
        await using var contexto = await fabrica.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var candidatos = await (
                from chunk in contexto.Chunks
                join documento in contexto.Documentos on chunk.DocumentoId equals documento.Id
                where chunk.Embedding != null
                      && documento.TipoDocumento == parametros.TipoDocumento
                      && documento.Modelo == parametros.Modelo
                      && documento.VersaoLayout == parametros.VersaoLayout
                      && documento.VigenciaInicio <= dataReferencia
                      && (documento.VigenciaFim == null || documento.VigenciaFim >= dataReferencia)
                orderby chunk.Embedding!.CosineDistance(embeddingConsulta)
                select new Candidato(chunk, documento, chunk.Embedding!.CosineDistance(embeddingConsulta)))
            .Take(limite * 4)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidatos.Count == 0)
        {
            return new RespostaBusca("0.1", []);
        }

        var regras = parametros.RegrasRelacionadas ?? [];
        var trechos = candidatos
            .Select(c =>
            {
                var similaridade = Math.Max(0, 1.0 - c.Distancia);
                var boost = regras.Length == 0
                    ? 0
                    : regras.Count(r => c.Chunk.RegrasRelacionadas.Contains(r, StringComparer.Ordinal)) * BoostPorRegra;
                var pontuacao = Math.Min(1.0, similaridade + boost);
                return new TrechoRanqueado(
                    c.Chunk,
                    c.Documento,
                    pontuacao);
            })
            .OrderByDescending(t => t.Pontuacao)
            .Take(limite)
            .Select(t => new TrechoResposta(
                t.Chunk.ChunkId,
                t.Documento.Titulo,
                t.Documento.VersaoDocumento,
                new VigenciaResposta(
                    t.Documento.VigenciaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    t.Documento.VigenciaFim?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                string.IsNullOrWhiteSpace(t.Chunk.Secao) && t.Chunk.Pagina is null
                    ? null
                    : new LocalizacaoResposta(t.Chunk.Secao, t.Chunk.Pagina),
                t.Chunk.Trecho,
                t.Pontuacao))
            .ToList();

        return new RespostaBusca("0.1", trechos);
    }

    private sealed record Candidato(Infraestrutura.Entidades.ChunkIndexado Chunk, Infraestrutura.Entidades.DocumentoOficial Documento, double Distancia);

    private sealed record TrechoRanqueado(Infraestrutura.Entidades.ChunkIndexado Chunk, Infraestrutura.Entidades.DocumentoOficial Documento, double Pontuacao);
}
