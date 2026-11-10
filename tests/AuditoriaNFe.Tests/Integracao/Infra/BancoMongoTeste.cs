using AuditoriaNFe.Infrastructure.Persistencia.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AuditoriaNFe.Tests.Integracao.Infra;

/// <summary>Leitura direta das coleções, como o banco as guarda, para conferir o que foi gravado.</summary>
public static class BancoMongoTeste
{
    public static IMongoDatabase Banco(ContainerMongo mongo)
    {
        ArgumentNullException.ThrowIfNull(mongo);
        var url = MongoUrl.Create(mongo.ConnectionString);
        return new MongoClient(url).GetDatabase(url.DatabaseName ?? ContextoMongo.BancoPadrao);
    }

    public static IMongoCollection<BsonDocument> Colecao(ContainerMongo mongo, string nome) =>
        Banco(mongo).GetCollection<BsonDocument>(nome);

    public static BsonBinaryData Guid(Guid id) => new(id, GuidRepresentation.Standard);
}
