using Collector.Core.Platform;

namespace Collector.Adapting;

public sealed record AdaptContext
{
    public Guid ProjectId { get; init; }
    public Guid ProjectSeed { get; init; }
    public string Namespace { get; init; } = "";
    public string Provider { get; init; } = "";
    public required Source Source { get; init; }
    public DateTime AdaptedAt { get; init; }
}
