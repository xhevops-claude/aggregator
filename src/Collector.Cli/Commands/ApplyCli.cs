using Collector.Storing;

namespace Collector.Cli.Commands;

public static class ApplyCli
{
    public static async Task<int> RunAsync(CliContext cli, CliArgs args, CancellationToken cancellationToken)
    {
        var project = await cli.ProjectAsync(cancellationToken);
        var writer = new StoreWriter(cli.Db, cli.Time);
        var batch = args.Int("batch") ?? 500;
        if (batch <= 0) throw new CliException("--batch must be a positive number");

        int applied = 0, rejected = 0;
        var stillPending = new Dictionary<Guid, ApplyResult>();
        while (true)
        {
            var limit = batch + stillPending.Count;
            var report = await writer.ApplyPendingAsync(project.Id, null, limit, cancellationToken);
            var progressed = false;
            foreach (var result in report.Results)
            {
                var settled = result.Outcome is ApplyOutcome.Applied or ApplyOutcome.Rejected;
                if (settled)
                {
                    stillPending.Remove(result.StagingId);
                    if (result.Outcome == ApplyOutcome.Applied) applied++;
                    else rejected++;
                }

                var seen = stillPending.ContainsKey(result.StagingId);
                if (!settled) stillPending[result.StagingId] = result;
                progressed |= settled || !seen;
            }

            if (!progressed) break;
        }

        var failures = stillPending.Values.Where(result => result.Outcome == ApplyOutcome.Failed).ToList();
        Console.WriteLine($"applied {applied}, rejected {rejected}, failed {failures.Count}");
        foreach (var failure in failures) Console.WriteLine($"  failed {failure.StagingId}: {failure.Reason}");
        return failures.Count == 0 ? 0 : 3;
    }
}
