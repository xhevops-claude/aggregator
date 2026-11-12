using Microsoft.Extensions.Time.Testing;

namespace Collector.Harvesting.Tests.Fakes;

internal sealed class TrackingTimeProvider : FakeTimeProvider
{
    private readonly Lock gate = new();
    private readonly Dictionary<TrackedTimer, DateTimeOffset> armed = [];

    public TrackingTimeProvider(DateTimeOffset start)
        : base(start)
    {
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new TrackedTimer(this, callback);
        timer.Arm(dueTime, period);
        timer.Inner = base.CreateTimer(timer.Fire, state, dueTime, period);
        return timer;
    }

    public DateTimeOffset? NextDue()
    {
        lock (gate)
        {
            return armed.Count == 0 ? null : armed.Values.Min();
        }
    }

    public void Set(TrackedTimer timer, DateTimeOffset? dueAt)
    {
        lock (gate)
        {
            if (dueAt is { } due) armed[timer] = due;
            else armed.Remove(timer);
        }
    }
}
