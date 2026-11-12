using Collector.Data;
using MongoDB.Driver;

namespace Collector.Conducting.Tests;

public sealed class RigDatabase : IAsyncLifetime
{
    private const string DefaultConnection = "mongodb://localhost:27117/?replicaSet=rs0";

    private readonly MongoClient client;

    public RigDatabase()
    {
        MongoMapping.Register();
        var connection = Environment.GetEnvironmentVariable("COLLECTOR_MONGO") ?? DefaultConnection;
        client = new MongoClient(connection);
        Database = new CollectorDatabase(client.GetDatabase(Name));
    }

    public string Name { get; } = $"collector-conducting-tests-{Guid.NewGuid():N}";

    public CollectorDatabase Database { get; }

    public async Task InitializeAsync()
    {
        await IndexSetup.EnsureAsync(Database.Database, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await client.DropDatabaseAsync(Name);
        client.Dispose();
    }
}
