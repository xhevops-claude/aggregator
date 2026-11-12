using Collector.Core.Messages;
using Collector.Messaging;

namespace Collector.Workers;

public sealed class LakeSignalService(WorkerSettings settings, ILogger<LakeSignalService> log)
    : QueueWorker<LakeItemStored>(settings, log)
{
    protected override string Role => WorkerRoles.Conductor;
    protected override string Queue => QueueNames.ConductorLakeItemStored;

    protected override Func<LakeItemStored, CancellationToken, Task> CreateHandler(IMessagePublisher publisher)
    {
        return Note;
    }

    private Task Note(LakeItemStored message, CancellationToken token)
    {
        Log.LogDebug("Run {Run} stored {Key} v{Version}", message.RunId, message.Key, message.Version);
        return Task.CompletedTask;
    }
}
