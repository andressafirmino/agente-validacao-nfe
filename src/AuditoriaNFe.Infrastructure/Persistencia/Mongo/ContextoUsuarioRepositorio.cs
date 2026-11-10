using AuditoriaNFe.Application.Abstracoes;
using AuditoriaNFe.Domain.ContextoUsuario;
using MongoDB.Driver;

namespace AuditoriaNFe.Infrastructure.Persistencia.Mongo;

/// <summary>
/// Coleção <c>contextos_usuario</c> (data-model §13): <c>_id</c> é a chave de isolamento (FR-056).
/// A leitura ignora o contexto vencido mesmo antes da remoção pelo índice TTL (FR-060); a gravação
/// confere <c>atualizadoEm</c> da leitura para não sobrescrever uma gravação concorrente.
/// </summary>
public sealed class ContextoUsuarioRepositorio(ContextoMongo contexto) : IContextoUsuarioRepositorio
{
    private const int CodigoChaveDuplicada = 11000;

    private static FilterDefinitionBuilder<ContextoUsuario> Filtro => Builders<ContextoUsuario>.Filter;

    public Task<ContextoUsuario?> ObterAsync(string chaveIsolamento, DateTimeOffset agora, CancellationToken cancellationToken) =>
        contexto.ExecutarAsync<ContextoUsuario?>(
            async token => await contexto.ContextosUsuario
                .Find(Filtro.Eq(c => c.Id, chaveIsolamento) & Filtro.Gt(c => c.ExpiraEm, agora))
                .FirstOrDefaultAsync(token)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<bool> SalvarAsync(ContextoUsuario contextoUsuario, DateTimeOffset? atualizadoEmLido, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextoUsuario);
        var filtro = atualizadoEmLido is { } lido
            ? Filtro.Eq(c => c.Id, contextoUsuario.Id) & Filtro.Eq(c => c.AtualizadoEm, lido)
            : Filtro.Eq(c => c.Id, contextoUsuario.Id) & Filtro.Lte(c => c.ExpiraEm, contextoUsuario.CriadoEm);
        return contexto.ExecutarAsync(
            async token =>
            {
                try
                {
                    var resultado = await contexto.ContextosUsuario
                        .ReplaceOneAsync(filtro, contextoUsuario, new ReplaceOptions { IsUpsert = atualizadoEmLido is null }, token)
                        .ConfigureAwait(false);
                    return resultado.MatchedCount > 0 || resultado.UpsertedId is not null;
                }
                catch (MongoWriteException erro) when (erro.WriteError?.Code == CodigoChaveDuplicada)
                {
                    return false;
                }
            },
            cancellationToken);
    }

    public Task ExcluirAsync(string chaveIsolamento, CancellationToken cancellationToken) =>
        contexto.ExecutarAsync(
            token => contexto.ContextosUsuario.DeleteOneAsync(Filtro.Eq(c => c.Id, chaveIsolamento), token),
            cancellationToken);
}
