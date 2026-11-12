using Collector.Core.Entities;

namespace Collector.Storing;

public sealed record ApplyResult
{
    public Guid StagingId { get; init; }
    public ApplyOutcome Outcome { get; init; }
    public EntityKind? Kind { get; init; }
    public Guid? EntityId { get; init; }
    public string? Reason { get; init; }
}
