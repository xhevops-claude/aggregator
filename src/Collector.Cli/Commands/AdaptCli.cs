using Collector.Adapting;
using Collector.Core.Messages;
using Collector.Providers.Nbshop;
using Collector.Providers.SportM;

namespace Collector.Cli.Commands;

public static class AdaptCli
{
    private const string ErrorClass = "adapt";

    public static async Task<int> RunAsync(CliContext cli, CliArgs args, CancellationToken cancellationToken)
    {
        var path = args.At(1, "operation path, e.g. clothing/shoes/sportm");

        var project = await cli.ProjectAsync(cancellationToken);
        var leaf = await cli.LeafAsync(project, path, cancellationToken);
        var catalog = new AdapterCatalog([.. NbshopModule.Adapters, .. SportMModule.Adapters]);
        var adapter = catalog.Get(leaf.Source.Platform, leaf.Domain);

        var publisher = await cli.PublisherAsync(cancellationToken);
        var runner = new AdaptRunner(cli.Lake(), cli.Db, publisher, cli.Time);

        var run = await RunRecords.StartAsync(cli, project.Id, leaf.Operation.Path, adapter.Name, cancellationToken);
        var command = new AdaptCommand
        {
            ProjectId = project.Id,
            RunId = run.RunId,
            ItemId = run.ItemId,
            Namespace = leaf.Operation.Path,
            Command = adapter.Name,
            LakeKeys = [.. args.Options("key")],
        };

        var input = new AdaptInput
        {
            Command = command,
            Operation = leaf.Operation,
            Domain = leaf.Domain,
            Source = leaf.Source,
            Project = project,
            Adapter = adapter,
        };

        var report = await RunRecords.TrackAsync(cli, run, Adapt, Outcome, cancellationToken);
        var succeeded = report.Failures.Count == 0;

        Console.WriteLine($"run {run.RunId}");
        Console.WriteLine($"raw items {report.RawItems}, staged {report.Staged}, rejected {report.Rejected}");
        foreach (var failure in report.Failures) Console.WriteLine($"  failed {failure}");
        return succeeded ? 0 : 3;

        Task<AdaptReport> Adapt()
        {
            return runner.RunAsync(input, cancellationToken);
        }
    }

    private static RunOutcome Outcome(AdaptReport report)
    {
        var succeeded = report.Failures.Count == 0;
        return RunOutcome.Completed(succeeded, ErrorClass, succeeded ? null : "adapt failures");
    }
}
