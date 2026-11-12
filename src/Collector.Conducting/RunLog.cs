using Collector.Core.Runs;
using Collector.Data;

namespace Collector.Conducting;

public sealed class RunLog(CollectorDatabase db, TimeProvider time)
{
    public const string Conductor = "conductor";

    public Task AddAsync(Run run, string type, string detail, CancellationToken token)
    {
        var entry = new RunEvent
        {
            Id = Guid.NewGuid(),
            ProjectId = run.ProjectId,
            RunId = run.Id,
            At = time.GetUtcNow().UtcDateTime,
            Type = type,
            Detail = detail,
            By = Conductor,
        };
        return db.RunEvents.InsertOneAsync(entry, cancellationToken: token);
    }
}
