namespace Collector.Messaging;

public sealed record ConsumerOptions
{
    public string Queue { get; init; } = "";
    public ushort? Prefetch { get; init; }
    public int MaxAttempts { get; init; } = 5;
    public TimeSpan FirstRetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(10);

    public ushort PrefetchOrTopologyDefault()
    {
        return Prefetch ?? TopologySetup.PrefetchFor(Queue);
    }

    public TimeSpan RetryDelayAfter(int failedAttempt)
    {
        var doublings = Math.Max(0, failedAttempt - 1);
        var milliseconds = FirstRetryDelay.TotalMilliseconds * Math.Pow(2, doublings);
        return TimeSpan.FromMilliseconds(Math.Min(milliseconds, MaxRetryDelay.TotalMilliseconds));
    }
}
