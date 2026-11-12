namespace Collector.Core.Runs;

public sealed record RunItem
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public Guid CommandId { get; init; }
    public RunItemState State { get; init; }
    public int Attempts { get; init; }
    public string? Node { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public string? ErrorClass { get; init; }
    public string? ErrorText { get; init; }
    public string? LakeKey { get; init; }
    public List<string> LakeKeys { get; init; } = [];
    public List<Guid> EntityIds { get; init; } = [];
}
