using Collector.Core.Messages;
using Collector.Data;
using Collector.Harvesting;
using Collector.Lake;
using Collector.Messaging;
using Collector.Providers.Nbshop;
using Collector.Providers.SportM;
using MongoDB.Driver;

namespace Collector.Workers;

public sealed class HarvesterService(WorkerSettings settings, CollectorDatabase db, ILogger<HarvesterService> log)
    : QueueWorker<HarvestCommand>(settings, log)
{
    private readonly TimeProvider time = TimeProvider.System;
    private readonly PoliteFetcher fetcher = new(new FetcherOptions(), TimeProvider.System);

    protected override string Role => WorkerRoles.Harvester;
    protected override string Queue => QueueNames.HarvesterHarvestCommand;

    protected override Func<HarvestCommand, CancellationToken, Task> CreateHandler(IMessagePublisher publisher)
    {
        var catalog = new HarvestCommandCatalog([.. NbshopModule.HarvestCommands, .. SportMModule.HarvestCommands]);
        var lake = new LakeStore(db, Settings.Lake);
        var runner = new HarvestRunner(catalog, fetcher, new HostBudget(time), lake, publisher, time);
        return (message, token) => HarvestAsync(runner, message, token);
    }

    public override void Dispose()
    {
        fetcher.Dispose();
        base.Dispose();
    }

    private async Task HarvestAsync(HarvestRunner runner, HarvestCommand message, CancellationToken token)
    {
        var project = await db.Projects.Find(p => p.Id == message.ProjectId).FirstOrDefaultAsync(token);
        if (project is null) throw new InvalidOperationException($"Unknown project {message.ProjectId}");

        var sources = db.Sources.Find(s => s.ProjectId == message.ProjectId && s.Key == message.Source);
        var source = await sources.FirstOrDefaultAsync(token);
        if (source is null) throw new InvalidOperationException($"Unknown source {message.Source}");

        Log.LogInformation("Harvest {Command} on {Source}, run {Run}", message.Command, source.Key, message.RunId);
        var report = await runner.RunAsync(message, source, project.LakeRetention, token);

        var stored = report.Stored.Count;
        var unchanged = report.Unchanged.Count;
        var failed = report.Failed.Count;
        Log.LogInformation("Harvested run {Run}: stored {Stored}, unchanged {Unchanged}, failed {Failed}",
            message.RunId, stored, unchanged, failed);
    }
}
