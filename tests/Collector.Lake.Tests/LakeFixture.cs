using Collector.Data;
using MongoDB.Driver;

namespace Collector.Lake.Tests;

public sealed class LakeFixture : IAsyncLifetime
{
    private const string DefaultMongo = "mongodb://localhost:27117/?replicaSet=rs0";
    private const string DefaultLake = "main";

    private readonly MongoClient client;

    public LakeFixture()
    {
        MongoMapping.Register();
        var connection = Environment.GetEnvironmentVariable("COLLECTOR_MONGO") ?? DefaultMongo;
        client = new MongoClient(connection);
        DatabaseName = $"collector_lake_tests_{Guid.NewGuid():N}";
        Database = new CollectorDatabase(client.GetDatabase(DatabaseName));
        Root = Path.Combine(Path.GetTempPath(), "collector-lake-tests", Guid.NewGuid().ToString("N"));
    }

    public string DatabaseName { get; }
    public CollectorDatabase Database { get; }
    public string Root { get; }

    public LakeStore NewStore() => NewStore(DefaultLake);

    public LakeStore NewStore(string lakeName) => NewStore(lakeName, LakeOptions.DefaultUnindexedBodyWait);

    public LakeStore NewStore(string lakeName, TimeSpan unindexedBodyWait)
    {
        var options = new LakeOptions { Root = Path.Combine(Root, lakeName), UnindexedBodyWait = unindexedBodyWait };
        return new LakeStore(Database, options);
    }

    public async Task InitializeAsync()
    {
        await IndexSetup.EnsureAsync(Database.Database, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await client.DropDatabaseAsync(DatabaseName);
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        client.Dispose();
    }
}
