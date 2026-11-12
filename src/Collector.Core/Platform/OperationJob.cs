namespace Collector.Core.Platform;

public sealed record OperationJob
{
    public Guid JobId { get; init; }
    public Guid StrategyId { get; init; }
    public string? Schedule { get; init; }
}
