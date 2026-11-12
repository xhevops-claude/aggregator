using Collector.Data;
using MongoDB.Driver;

namespace Collector.Cli.Seeding;

public static class Seeder
{
    private static readonly ReplaceOptions Upsert = new() { IsUpsert = true };

    public static async Task<string> SeedShoesAsync(CollectorDatabase db, CancellationToken cancellationToken)
    {
        var project = ShoesProject.Project();
        await db.Projects.ReplaceOneAsync(p => p.Id == project.Id, project, Upsert, cancellationToken);

        foreach (var shop in ShoesProject.Shops)
        {
            var provider = ShoesProject.Provider(shop);
            await db.Providers.ReplaceOneAsync(p => p.Id == provider.Id, provider, Upsert, cancellationToken);

            var source = ShoesProject.Source(shop);
            await db.Sources.ReplaceOneAsync(s => s.Id == source.Id, source, Upsert, cancellationToken);
        }

        foreach (var job in ShoesProject.Jobs())
        {
            await db.Jobs.ReplaceOneAsync(j => j.Id == job.Id, job, Upsert, cancellationToken);
        }

        var strategy = ShoesProject.DevStrategy();
        await db.Strategies.ReplaceOneAsync(s => s.Id == strategy.Id, strategy, Upsert, cancellationToken);

        var operations = ShoesProject.Operations();
        foreach (var operation in operations)
        {
            await db.Operations.ReplaceOneAsync(o => o.Id == operation.Id, operation, Upsert, cancellationToken);
        }

        var shops = ShoesProject.Shops.Count;
        return $"project {project.Name} ({project.Id}): {operations.Count} operations, {shops} providers and sources";
    }
}
