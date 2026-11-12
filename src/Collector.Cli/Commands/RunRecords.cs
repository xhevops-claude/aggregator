using Collector.Core.Runs;
using MongoDB.Driver;

namespace Collector.Cli.Commands;

public static class RunRecords
{
    public static async Task<RunHandle> StartAsync(
        CliContext cli, Guid projectId, string path, string command, CancellationToken cancellationToken)
    {
        var now = cli.Time.GetUtcNow().UtcDateTime;
        var handle = new RunHandle(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var run = new Run
        {
            Id = handle.RunId,
            ProjectId = projectId,
            Namespace = path,
            Parameters = new Dictionary<string, string> { ["command"] = command },
            StartedBy = "cli",
            State = RunState.Running,
            StartedAt = now,
        };
        await cli.Db.Runs.InsertOneAsync(run, cancellationToken: cancellationToken);

        var item = new RunItem
        {
            Id = handle.ItemId,
            ProjectId = projectId,
            RunId = handle.RunId,
            CommandId = handle.CommandId,
            State = RunItemState.Running,
            Attempts = 1,
            Node = Environment.MachineName,
            StartedAt = now,
        };
        await cli.Db.RunItems.InsertOneAsync(item, cancellationToken: cancellationToken);
        return handle;
    }

    public static async Task<T> TrackAsync<T>(
        CliContext cli, RunHandle handle, Func<Task<T>> work, Func<T, RunOutcome> outcomeOf,
        CancellationToken cancellationToken)
    {
        T result;
        try
        {
            result = await work();
        }
        catch (Exception exception)
        {
            await FinishAsync(cli, handle, RunOutcome.Aborted(exception, cancellationToken));
            throw;
        }

        await FinishAsync(cli, handle, outcomeOf(result));
        return result;
    }

    private static async Task FinishAsync(CliContext cli, RunHandle handle, RunOutcome outcome)
    {
        var now = cli.Time.GetUtcNow().UtcDateTime;

        var set = Builders<RunItem>.Update;
        var itemUpdate = set.Set(i => i.State, outcome.ItemState).Set(i => i.FinishedAt, now);
        itemUpdate = itemUpdate.Set(i => i.ErrorClass, outcome.ErrorClass).Set(i => i.ErrorText, outcome.ErrorText);
        var itemFilter = Builders<RunItem>.Filter.Eq(i => i.Id, handle.ItemId);
        await cli.Db.RunItems.UpdateOneAsync(itemFilter, itemUpdate, cancellationToken: CancellationToken.None);

        var runUpdate = Builders<Run>.Update.Set(r => r.State, outcome.RunState);
        var paused = outcome.RunState == RunState.WaitingForDecision;
        if (!paused) runUpdate = runUpdate.Set(r => r.FinishedAt, now);
        var runFilter = Builders<Run>.Filter.Eq(r => r.Id, handle.RunId);
        await cli.Db.Runs.UpdateOneAsync(runFilter, runUpdate, cancellationToken: CancellationToken.None);
    }
}
