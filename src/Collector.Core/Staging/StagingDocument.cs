using Collector.Core.Entities;

namespace Collector.Core.Staging;

public sealed record StagingDocument
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public EntityKind Kind { get; init; }
    public Guid EntityId { get; init; }
    public string LakeKey { get; init; } = "";
    public int LakeVersion { get; init; }
    public DateTime AdaptedAt { get; init; }
    public StagingState State { get; init; }
    public string? Reason { get; init; }
    public required Entity Entity { get; init; }
}
