namespace Collector.Harvesting;

internal sealed class HostState
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public SemaphoreSlim RobotsGate { get; } = new(1, 1);
    public DateTimeOffset NextAllowed { get; set; } = DateTimeOffset.MinValue;
    public DateTimeOffset HeldUntil { get; set; } = DateTimeOffset.MinValue;
    public RobotsRules? Robots { get; set; }
    public RobotsFailure? RobotsFailure { get; set; }
}
