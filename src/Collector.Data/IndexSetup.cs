using MongoDB.Bson;
using MongoDB.Driver;

namespace Collector.Data;

public static class IndexSetup
{
    public static IReadOnlyList<CollectionIndex> All { get; } =
    [
        new(CollectionNames.Projects, "{ name: 1 }", Unique: true),
        new(CollectionNames.Operations, "{ projectId: 1, path: 1 }", Unique: true),
        new(CollectionNames.Operations, "{ projectId: 1, parentId: 1 }"),
        new(CollectionNames.Providers, "{ projectId: 1, key: 1 }", Unique: true),
        new(CollectionNames.Sources, "{ projectId: 1, key: 1 }", Unique: true),
        new(CollectionNames.Nodes, "{ projectId: 1, name: 1 }", Unique: true),
        new(CollectionNames.Jobs, "{ projectId: 1, name: 1 }", Unique: true),
        new(CollectionNames.Strategies, "{ projectId: 1, name: 1 }", Unique: true),
        new(CollectionNames.Runs, "{ projectId: 1, namespace: 1, startedAt: -1 }"),
        new(CollectionNames.Runs, "{ projectId: 1, state: 1 }"),
        new(CollectionNames.RunCommands, "{ projectId: 1, runId: 1, order: 1 }", Unique: true),
        new(CollectionNames.RunItems, "{ projectId: 1, runId: 1, commandId: 1, state: 1 }"),
        new(CollectionNames.RunEvents, "{ projectId: 1, runId: 1, at: 1 }"),
        new(CollectionNames.LakeItems, "{ projectId: 1, key: 1, version: -1 }", Unique: true),
        new(CollectionNames.LakeItems, "{ projectId: 1, source: 1, harvestedAt: -1 }"),
        new(CollectionNames.Staging, "{ projectId: 1, runId: 1, state: 1 }"),
        new(CollectionNames.Staging, "{ projectId: 1, entityId: 1 }"),
        new(CollectionNames.Products, "{ projectId: 1, sku: 1 }"),
        new(CollectionNames.Products, "{ projectId: 1, namespace: 1 }"),
        new(CollectionNames.Products, "{ projectId: 1, aliases: 1 }"),
        new(CollectionNames.Listings, "{ projectId: 1, productId: 1 }"),
        new(CollectionNames.Listings, "{ projectId: 1, namespace: 1, stockCheckedAt: 1 }"),
        new(CollectionNames.Listings, "{ projectId: 1, namespace: 1, infoCheckedAt: 1 }"),
        new(CollectionNames.Categories, "{ projectId: 1, namespace: 1, path: 1 }"),
    ];

    public static async Task<int> EnsureAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        foreach (var group in All.GroupBy(index => index.Collection))
        {
            var collection = database.GetCollection<BsonDocument>(group.Key);
            var models = group.Select(index => index.ToModel()).ToList();
            await collection.Indexes.CreateManyAsync(models, cancellationToken);
        }

        return All.Count;
    }
}
