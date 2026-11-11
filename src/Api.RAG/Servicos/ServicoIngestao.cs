using System.Security.Cryptography;
using System.Text;
using Api.RAG.Contratos;
using Api.RAG.Infraestrutura;
using Api.RAG.Infraestrutura.Embeddings;
using Api.RAG.Infraestrutura.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Api.RAG.Configuracao;

namespace Api.RAG.Servicos;

public sealed class ServicoIngestao(
    IDbContextFactory<RagDbContext> fabrica,
    SegmentadorTexto segmentador,
    IProvedorEmbeddings embeddings,
    IOptions<RagOpcoes> opcoes,
    ILogger<ServicoIngestao> log)
{
    public sealed record ResultadoIngestao(Guid JobId, Guid DocumentoId, int ChunksIndexados, bool Idempotente);

    public async Task<ResultadoIngestao> IngerirAsync(IngestaoDocumentoRagV1 pedido, string responsavel, CancellationToken cancellationToken)
    {
        var hash = HashConteudo(pedido.ConteudoTexto);
        await using var contexto = await fabrica.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var existente = await contexto.Documentos
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.DocumentoIdExterno == pedido.DocumentoIdExterno
                     && d.VersaoDocumento == pedido.VersaoDocumento
                     && d.HashConteudo == hash,
                cancellationToken)
            .ConfigureAwait(false);

        if (existente is not null)
        {
            var quantidade = await contexto.Chunks.CountAsync(c => c.DocumentoId == existente.Id, cancellationToken).ConfigureAwait(false);
            if (quantidade > 0)
            {
                var jobAntigo = await contexto.JobsIngestao
                    .Where(j => j.HashConteudo == hash && j.Estado == EstadoJobIngestao.Concluido)
                    .OrderByDescending(j => j.ConcluidoEm)
                    .Select(j => j.Id)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                return new ResultadoIngestao(jobAntigo == Guid.Empty ? Guid.CreateVersion7() : jobAntigo, existente.Id, quantidade, true);
            }
        }

        var jobId = Guid.CreateVersion7();
        var documentoId = Guid.CreateVersion7();
        var job = new JobIngestao
        {
            Id = jobId,
            Estado = EstadoJobIngestao.Processando,
            DocumentoIdExterno = pedido.DocumentoIdExterno,
            VersaoDocumento = pedido.VersaoDocumento,
            HashConteudo = hash,
            IniciadoEm = DateTimeOffset.UtcNow,
        };

        contexto.JobsIngestao.Add(job);
        await contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var trechos = segmentador.Segmentar(pedido.ConteudoTexto);
            if (trechos.Count == 0)
            {
                throw new InvalidOperationException("Nenhum trecho gerado a partir do conteúdo.");
            }

            var regrasPadrao = pedido.RegrasRelacionadas ?? [];
            var chunks = new List<ChunkIndexado>(trechos.Count);
            var chamadas = new List<ChamadaEmbedding>(trechos.Count);
            var sequencia = 0;

            foreach (var trecho in trechos)
            {
                sequencia++;
                ResultadoEmbedding embedding;
                try
                {
                    embedding = await embeddings.GerarEmbeddingAsync(trecho, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception erro) when (erro is not OperationCanceledException)
                {
                    log.LogWarning(erro, "Falha ao gerar embedding na ingestão do documento {DocumentoExterno}.", pedido.DocumentoIdExterno);
                    throw new FalhaEmbeddingException();
                }

                chunks.Add(new ChunkIndexado
                {
                    Id = Guid.CreateVersion7(),
                    ChunkId = MontarChunkId(pedido.DocumentoIdExterno, sequencia),
                    DocumentoId = documentoId,
                    Sequencia = sequencia,
                    Trecho = trecho,
                    RegrasRelacionadas = regrasPadrao,
                    PalavrasChave = [],
                    Embedding = new Vector(embedding.Vetor.ToArray()),
                });

                chamadas.Add(new ChamadaEmbedding
                {
                    Id = Guid.CreateVersion7(),
                    JobIngestaoId = jobId,
                    Modelo = opcoes.Value.ModeloEmbedding,
                    TokensEntrada = embedding.TokensEntrada,
                    RegistradoEm = DateTimeOffset.UtcNow,
                });
            }

            await using var transacao = await contexto.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var mesmoExterno = await contexto.Documentos
                .Where(d => d.DocumentoIdExterno == pedido.DocumentoIdExterno && d.VersaoDocumento == pedido.VersaoDocumento)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (mesmoExterno.Count > 0)
            {
                contexto.Documentos.RemoveRange(mesmoExterno);
            }

            var documento = new DocumentoOficial
            {
                Id = documentoId,
                DocumentoIdExterno = pedido.DocumentoIdExterno,
                Titulo = pedido.Titulo,
                TipoDocumento = pedido.TipoDocumento,
                Modelo = pedido.Modelo,
                VersaoLayout = pedido.VersaoLayout,
                VersaoDocumento = pedido.VersaoDocumento,
                VigenciaInicio = ValidadorIngestaoRag.ParseData(pedido.VigenciaInicio),
                VigenciaFim = ValidadorIngestaoRag.ParseDataOpcional(pedido.VigenciaFim),
                HashConteudo = hash,
                Fonte = pedido.Fonte,
                IngestidoEm = DateTimeOffset.UtcNow,
                Responsavel = responsavel,
            };

            contexto.Documentos.Add(documento);
            contexto.Chunks.AddRange(chunks);
            contexto.ChamadasEmbedding.AddRange(chamadas);

            job.Estado = EstadoJobIngestao.Concluido;
            job.ConcluidoEm = DateTimeOffset.UtcNow;
            contexto.JobsIngestao.Update(job);

            await contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transacao.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new ResultadoIngestao(jobId, documentoId, chunks.Count, false);
        }
        catch (FalhaEmbeddingException)
        {
            await MarcarJobFalhouAsync(contexto, job, "Falha ao gerar embeddings.", cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await MarcarJobFalhouAsync(contexto, job, "Falha na ingestão.", cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static string HashConteudo(string texto)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string MontarChunkId(string documentoIdExterno, int sequencia)
    {
        var slug = new string(documentoIdExterno
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return $"{slug}-{sequencia:000}";
    }

    private static async Task MarcarJobFalhouAsync(
        RagDbContext contexto,
        JobIngestao job,
        string mensagem,
        CancellationToken cancellationToken)
    {
        job.Estado = EstadoJobIngestao.Falhou;
        job.MensagemErro = mensagem;
        job.ConcluidoEm = DateTimeOffset.UtcNow;
        contexto.JobsIngestao.Update(job);
        await contexto.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public sealed class FalhaEmbeddingException : Exception;
}
