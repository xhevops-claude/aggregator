namespace Collector.Core.Platform;

public sealed record Provider
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
}
