using Collector.Conducting;
using Collector.Core.Messages;
using Collector.Data;
using Collector.Messaging;

namespace Collector.Workers;

public sealed class RunRequestService(WorkerSettings settings, CollectorDatabase db, ILogger<RunRequestService> log)
    : QueueWorker<RunRequested>(settings, log)
{
    protected override string Role => WorkerRoles.Conductor;
    protected override string Queue => QueueNames.ConductorRunRequested;

    protected override Func<RunRequested, CancellationToken, Task> CreateHandler(IMessagePublisher publisher)
    {
        var conductor = new Conductor(db, publisher, TimeProvider.System);
        return (message, token) => StartAsync(conductor, message, token);
    }

    private async Task StartAsync(Conductor conductor, RunRequested message, CancellationToken token)
    {
        var run = await conductor.StartAsync(message, token);
        Log.LogInformation("Run {Run} started: {Job} on {Namespace}", run.Id, message.Job, run.Namespace);
    }
}
