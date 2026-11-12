namespace Collector.Core.Platform;

public sealed record Project
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public Guid Seed { get; init; }
    public LakeRetention LakeRetention { get; init; }
}
