namespace Collector.Harvesting.Tests.Fakes;

internal sealed class TrackedTimer : ITimer
{
    private readonly TrackingTimeProvider owner;
    private readonly TimerCallback callback;
    private TimeSpan period = Timeout.InfiniteTimeSpan;

    public TrackedTimer(TrackingTimeProvider owner, TimerCallback callback)
    {
        this.owner = owner;
        this.callback = callback;
    }

    public ITimer? Inner { get; set; }

    public void Arm(TimeSpan dueTime, TimeSpan period)
    {
        this.period = period;
        var dueAt = dueTime == Timeout.InfiniteTimeSpan ? (DateTimeOffset?)null : owner.GetUtcNow() + dueTime;
        owner.Set(this, dueAt);
    }

    public void Fire(object? state)
    {
        var repeats = period != Timeout.InfiniteTimeSpan && period > TimeSpan.Zero;
        owner.Set(this, repeats ? owner.GetUtcNow() + period : null);
        callback(state);
    }

    public bool Change(TimeSpan dueTime, TimeSpan period)
    {
        Arm(dueTime, period);
        return Inner?.Change(dueTime, period) ?? false;
    }

    public void Dispose()
    {
        owner.Set(this, null);
        Inner?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
