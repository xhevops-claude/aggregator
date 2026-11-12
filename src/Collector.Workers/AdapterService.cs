using Collector.Adapting;
using Collector.Core.Messages;
using Collector.Data;
using Collector.Lake;
using Collector.Messaging;
using Collector.Providers.Nbshop;
using Collector.Providers.SportM;

namespace Collector.Workers;

public sealed class AdapterService(WorkerSettings settings, CollectorDatabase db, ILogger<AdapterService> log)
    : QueueWorker<AdaptCommand>(settings, log)
{
    private readonly AdapterCatalog catalog = new([.. NbshopModule.Adapters, .. SportMModule.Adapters]);
    private readonly LeafResolver leaves = new(db);

    protected override string Role => WorkerRoles.Adapter;
    protected override string Queue => QueueNames.AdapterAdaptCommand;

    protected override Func<AdaptCommand, CancellationToken, Task> CreateHandler(IMessagePublisher publisher)
    {
        var runner = new AdaptRunner(new LakeStore(db, Settings.Lake), db, publisher, TimeProvider.System);
        return (message, token) => AdaptAsync(runner, message, token);
    }

    private async Task AdaptAsync(AdaptRunner runner, AdaptCommand message, CancellationToken token)
    {
        var leaf = await leaves.ResolveAsync(message.ProjectId, message.Namespace, token);
        var adapter = catalog.Get(leaf.Source.Platform, leaf.Domain);
        if (message.Command.Length > 0 && message.Command != adapter.Name)
        {
            var text = $"{message.Namespace} adapts with {adapter.Name}, not {message.Command}";
            throw new InvalidOperationException(text);
        }

        var input = new AdaptInput
        {
            Command = message,
            Operation = leaf.Operation,
            Domain = leaf.Domain,
            Source = leaf.Source,
            Project = leaf.Project,
            Adapter = adapter,
        };

        var path = message.Namespace;
        Log.LogInformation("Adapt {Namespace} with {Adapter}, run {Run}", path, adapter.Name, message.RunId);
        var report = await runner.RunAsync(input, token);

        var failed = report.Failures.Count;
        Log.LogInformation("Adapted run {Run}: raw {Raw}, staged {Staged}, rejected {Rejected}, failed {Failed}",
            message.RunId, report.RawItems, report.Staged, report.Rejected, failed);
    }
}
