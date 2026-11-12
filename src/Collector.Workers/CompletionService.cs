using Collector.Conducting;
using Collector.Core.Messages;
using Collector.Data;
using Collector.Messaging;

namespace Collector.Workers;

public sealed class CompletionService(WorkerSettings settings, CollectorDatabase db, ILogger<CompletionService> log)
    : QueueWorker<ItemCompleted>(settings, log)
{
    protected override string Role => WorkerRoles.Conductor;
    protected override string Queue => QueueNames.ConductorItemCompleted;

    protected override Func<ItemCompleted, CancellationToken, Task> CreateHandler(IMessagePublisher publisher)
    {
        var conductor = new Conductor(db, publisher, TimeProvider.System);
        return conductor.OnItemCompletedAsync;
    }
}
