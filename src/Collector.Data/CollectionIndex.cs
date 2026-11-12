using MongoDB.Bson;
using MongoDB.Driver;

namespace Collector.Data;

public sealed record CollectionIndex(string Collection, string Keys, bool Unique = false)
{
    public CreateIndexModel<BsonDocument> ToModel()
    {
        var keys = new BsonDocumentIndexKeysDefinition<BsonDocument>(BsonDocument.Parse(Keys));
        return new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Unique = Unique });
    }
}
