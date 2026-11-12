using Collector.Core.Messages;
using Collector.Core.Runs;
using Collector.Data;
using Collector.Messaging;
using Collector.Storing;

namespace Collector.Workers;

public sealed class StoreWriterService(WorkerSettings settings, CollectorDatabase db, ILogger<StoreWriterService> log)
    : QueueWorker<EntityAdapted>(settings, log)
{
    public const string RejectedClass = "rejected";

    private readonly StoreWriter writer = new(db, TimeProvider.System);

    protected override string Role => WorkerRoles.StoreWriter;
    protected override string Queue => QueueNames.StoreWriterEntityAdapted;

    protected override Func<EntityAdapted, CancellationToken, Task> CreateHandler(IMessagePublisher publisher)
    {
        return (message, token) => ApplyAsync(publisher, message, token);
    }

    private async Task ApplyAsync(IMessagePublisher publisher, EntityAdapted message, CancellationToken token)
    {
        var result = await writer.ApplyAsync(message.ProjectId, message.StagingId, token);
        if (result.Outcome == ApplyOutcome.Failed)
        {
            throw new InvalidOperationException($"Staging {message.StagingId} failed: {result.Reason}");
        }

        var noteworthy = result.Outcome is ApplyOutcome.Applied or ApplyOutcome.Rejected;
        var level = noteworthy ? LogLevel.Information : LogLevel.Debug;
        Log.Log(level, "Staging {Staging}: {Outcome} {Reason}", message.StagingId, result.Outcome, result.Reason);

        if (result.Outcome == ApplyOutcome.NotFound) return;
        await publisher.PublishAsync(MessageNames.ItemCompleted, Completed(message, result), token);
    }

    private static ItemCompleted Completed(EntityAdapted message, ApplyResult result)
    {
        var rejected = result.Outcome == ApplyOutcome.Rejected;
        return new ItemCompleted
        {
            ProjectId = message.ProjectId,
            RunId = message.RunId,
            ItemId = message.StagingId,
            State = RunItemState.Done,
            ErrorClass = rejected ? RejectedClass : null,
            ErrorText = rejected ? result.Reason : null,
        };
    }
}
