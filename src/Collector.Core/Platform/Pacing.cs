namespace Collector.Core.Platform;

public sealed record Pacing
{
    public PacingMode Mode { get; init; } = PacingMode.Interval;
    public TimeSpan? Interval { get; init; }
    public TimeSpan? Window { get; init; }
    public TimeSpan? Jitter { get; init; }
    public int? MaxPerHour { get; init; }
}
