namespace Collector.Core.Platform;

public sealed record Node
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string Name { get; init; } = "";
    public List<Mechanism> Mechanisms { get; init; } = [];
    public string? Location { get; init; }
}
