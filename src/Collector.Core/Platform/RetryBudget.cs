namespace Collector.Core.Platform;

public sealed record RetryBudget
{
    public int AttemptsPerItem { get; init; } = 3;
    public TimeSpan Deadline { get; init; } = TimeSpan.FromHours(36);
    public double FailureRatio { get; init; } = 0.2;
    public int NodesTried { get; init; } = 2;
}
