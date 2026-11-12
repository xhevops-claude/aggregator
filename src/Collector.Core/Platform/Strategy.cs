namespace Collector.Core.Platform;

public sealed record Strategy
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string Name { get; init; } = "";
    public Mechanism Mechanism { get; init; }
    public NodeSelection Nodes { get; init; } = new();
    public Pacing Pacing { get; init; } = new();
    public AdaptMode Adapt { get; init; }
    public RetryBudget Retry { get; init; } = new();
}
