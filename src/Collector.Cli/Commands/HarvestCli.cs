using Collector.Cli.Seeding;
using Collector.Core.Entities;
using Collector.Core.Lake;
using Collector.Core.Messages;
using Collector.Core.Platform;
using Collector.Harvesting;
using Collector.Providers.Nbshop;
using Collector.Providers.SportM;
using MongoDB.Driver;

namespace Collector.Cli.Commands;

public static class HarvestCli
{
    private const string ProductPageKind = "product-page";

    public static async Task<int> RunAsync(CliContext cli, CliArgs args, CancellationToken cancellationToken)
    {
        var path = args.At(1, "operation path, e.g. clothing/shoes/sportm");
        var commandName = args.At(2, "harvest command, e.g. sportm.catalog-pages");
        var limit = args.Int("limit");

        var project = await cli.ProjectAsync(cancellationToken);
        var leaf = await cli.LeafAsync(project, path, cancellationToken);
        var strategyId = ShoesProject.IdFor("strategy", "dev");
        var strategy = await cli.Db.Strategies.Find(s => s.Id == strategyId).FirstOrDefaultAsync(cancellationToken);

        var targets = await TargetsAsync(cli, args, leaf, cancellationToken);
        var parameters = new Dictionary<string, string>();
        if (args.Option("listing") is { } listing) parameters[HarvestParameters.Listing] = listing;
        if (args.Option("brand") is { } brand) parameters[HarvestParameters.Brand] = brand;
        if (args.Option("contains") is { } contains) parameters[HarvestParameters.Contains] = contains;

        var catalog = new HarvestCommandCatalog([.. NbshopModule.HarvestCommands, .. SportMModule.HarvestCommands]);
        using var fetcher = new PoliteFetcher(new FetcherOptions(), cli.Time);
        var budget = new HostBudget(cli.Time);
        var publisher = await cli.PublisherAsync(cancellationToken);
        var runner = new HarvestRunner(catalog, fetcher, budget, cli.Lake(), publisher, cli.Time);

        var run = await RunRecords.StartAsync(cli, project.Id, leaf.Operation.Path, commandName, cancellationToken);
        var message = new HarvestCommand
        {
            ProjectId = project.Id,
            RunId = run.RunId,
            ItemId = run.ItemId,
            Namespace = leaf.Operation.Path,
            Command = commandName,
            Source = leaf.Source.Key,
            Targets = targets,
            Parameters = parameters,
            Limit = limit,
            Mechanism = Mechanism.Http,
            Pacing = strategy?.Pacing ?? new Pacing(),
        };

        var report = await RunRecords.TrackAsync(cli, run, Harvest, Outcome, cancellationToken);
        Print(run, report);
        return report.Succeeded ? 0 : 3;

        Task<HarvestReport> Harvest()
        {
            return runner.RunAsync(message, leaf.Source, project.LakeRetention, cancellationToken);
        }
    }

    private static RunOutcome Outcome(HarvestReport report)
    {
        var error = report.Error ?? $"{report.Failed.Count} of {report.Total} items failed";
        return RunOutcome.Completed(report.Succeeded, HarvestRunner.ErrorClass, error);
    }

    private static void Print(RunHandle run, HarvestReport report)
    {
        Console.WriteLine($"run {run.RunId}");
        var counts = $"stored {report.Stored.Count}, unchanged {report.Unchanged.Count}, failed {report.Failed.Count}";
        Console.WriteLine(counts);
        foreach (var item in report.Stored)
        {
            Console.WriteLine($"  stored {item.Kind} {item.ProviderId} v{item.Version}");
        }

        foreach (var failure in report.Failed)
        {
            var reason = $"{failure.ErrorClass} {failure.ErrorText}";
            Console.WriteLine($"  failed {failure.Kind} {failure.ProviderId}: {reason}");
        }

        if (report.Error is not null) Console.WriteLine($"error: {report.Error}");
    }

    private static async Task<List<HarvestTarget>> TargetsAsync(
        CliContext cli, CliArgs args, Leaf leaf, CancellationToken cancellationToken)
    {
        var targets = new List<HarvestTarget>();
        foreach (var value in args.Options("target"))
        {
            var parts = value.Split('=', 2);
            var url = parts.Length > 1 ? parts[1] : null;
            targets.Add(new HarvestTarget { ProviderId = parts[0], Url = url });
        }

        if (!args.Flag("from-store")) return targets;

        var limit = args.Int("limit") ?? 3;
        var projectId = leaf.Source.ProjectId;
        var path = leaf.Operation.Path;
        var listingFilter = Builders<Listing>.Filter;
        var withoutDetails = listingFilter.Where(l => l.ProjectId == projectId && l.Namespace == path);
        withoutDetails &= listingFilter.Eq(l => l.InfoCheckedAt, null);
        if (!args.Flag("retry"))
        {
            var harvested = await HarvestedProductPagesAsync(cli, leaf.Source, cancellationToken);
            withoutDetails &= listingFilter.Nin(l => l.ProviderId, harvested);
        }

        var find = cli.Db.Listings.Find(withoutDetails).SortBy(l => l.ProviderId);
        var listings = await find.Limit(limit).ToListAsync(cancellationToken);
        foreach (var listing in listings)
        {
            targets.Add(new HarvestTarget { ProviderId = listing.ProviderId, Url = listing.Url });
        }

        return targets;
    }

    private static async Task<List<string>> HarvestedProductPagesAsync(
        CliContext cli, Source source, CancellationToken cancellationToken)
    {
        var lakeFilter = Builders<LakeItem>.Filter;
        var pages = lakeFilter.Eq(i => i.ProjectId, source.ProjectId);
        pages &= lakeFilter.Eq(i => i.Source, source.Key);
        pages &= lakeFilter.Eq(i => i.Kind, ProductPageKind);
        var lake = cli.Db.LakeItems;
        var cursor = await lake.DistinctAsync(i => i.ProviderId, pages, cancellationToken: cancellationToken);
        return await cursor.ToListAsync(cancellationToken);
    }
}
