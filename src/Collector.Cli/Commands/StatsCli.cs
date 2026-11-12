using MongoDB.Bson;
using MongoDB.Driver;

namespace Collector.Cli.Commands;

public static class StatsCli
{
    public static async Task<int> RunAsync(CliContext cli, CancellationToken cancellationToken)
    {
        var project = await cli.ProjectAsync(cancellationToken);
        var byProject = new BsonDocument("projectId", new BsonBinaryData(project.Id, GuidRepresentation.Standard));

        await PrintGroupsAsync(cli, "lakeItems", byProject, "$source", "$kind", cancellationToken);
        await PrintGroupsAsync(cli, "staging", byProject, "$kind", "$state", cancellationToken);
        await PrintGroupsAsync(cli, "listings", byProject, "$namespace", "$availability", cancellationToken);
        await PrintGroupsAsync(cli, "products", byProject, "$namespace", "$brand", cancellationToken);

        var detailed = new BsonDocument(byProject) { ["infoCheckedAt"] = new BsonDocument("$exists", true) };
        var withDetails = await cli.Mongo.GetCollection<BsonDocument>("listings").CountDocumentsAsync(detailed);
        Console.WriteLine($"listings with details: {withDetails}");

        var shared = new BsonDocument(byProject) { ["aliases.1"] = new BsonDocument("$exists", true) };
        var matched = await cli.Mongo.GetCollection<BsonDocument>("products").CountDocumentsAsync(shared);
        Console.WriteLine($"products matched across two or more listings' providers: {matched}");
        return 0;
    }

    private static async Task PrintGroupsAsync(CliContext cli, string collection, BsonDocument match,
        string first, string second, CancellationToken cancellationToken)
    {
        var key = new BsonDocument { ["a"] = first, ["b"] = second };
        var group = new BsonDocument { ["_id"] = key, ["n"] = new BsonDocument("$sum", 1) };
        var pipeline = new[]
        {
            new BsonDocument("$match", match),
            new BsonDocument("$group", group),
            new BsonDocument("$sort", new BsonDocument("_id", 1)),
        };

        var documents = cli.Mongo.GetCollection<BsonDocument>(collection);
        var cursor = await documents.AggregateAsync<BsonDocument>(pipeline, cancellationToken: cancellationToken);
        var rows = await cursor.ToListAsync(cancellationToken);

        Console.WriteLine($"{collection}:");
        foreach (var row in rows)
        {
            var id = row["_id"].AsBsonDocument;
            Console.WriteLine($"  {id.GetValue("a", "-")} | {id.GetValue("b", "-")}: {row["n"]}");
        }
    }
}
