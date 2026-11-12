namespace Collector.Core.Runs;

public sealed record Run
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid? GroupId { get; init; }
    public string Namespace { get; init; } = "";
    public Guid JobId { get; init; }
    public Guid StrategyId { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = [];
    public string StartedBy { get; init; } = "";
    public RunState State { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}
