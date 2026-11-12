using Collector.Core.Entities;
using Collector.Core.Lake;
using Collector.Core.Platform;
using Collector.Core.Runs;
using Collector.Core.Staging;
using MongoDB.Driver;

namespace Collector.Data;

public sealed class CollectorDatabase(IMongoDatabase database)
{
    public IMongoDatabase Database { get; } = database;

    public IMongoCollection<Project> Projects => Get<Project>(CollectionNames.Projects);
    public IMongoCollection<Operation> Operations => Get<Operation>(CollectionNames.Operations);
    public IMongoCollection<Provider> Providers => Get<Provider>(CollectionNames.Providers);
    public IMongoCollection<Source> Sources => Get<Source>(CollectionNames.Sources);
    public IMongoCollection<Node> Nodes => Get<Node>(CollectionNames.Nodes);
    public IMongoCollection<Job> Jobs => Get<Job>(CollectionNames.Jobs);
    public IMongoCollection<Strategy> Strategies => Get<Strategy>(CollectionNames.Strategies);
    public IMongoCollection<Run> Runs => Get<Run>(CollectionNames.Runs);
    public IMongoCollection<RunCommand> RunCommands => Get<RunCommand>(CollectionNames.RunCommands);
    public IMongoCollection<RunItem> RunItems => Get<RunItem>(CollectionNames.RunItems);
    public IMongoCollection<RunEvent> RunEvents => Get<RunEvent>(CollectionNames.RunEvents);
    public IMongoCollection<LakeItem> LakeItems => Get<LakeItem>(CollectionNames.LakeItems);
    public IMongoCollection<StagingDocument> Staging => Get<StagingDocument>(CollectionNames.Staging);
    public IMongoCollection<Product> Products => Get<Product>(CollectionNames.Products);
    public IMongoCollection<Listing> Listings => Get<Listing>(CollectionNames.Listings);
    public IMongoCollection<Category> Categories => Get<Category>(CollectionNames.Categories);

    private IMongoCollection<T> Get<T>(string name) => Database.GetCollection<T>(name);
}
