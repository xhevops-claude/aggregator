namespace Collector.Core.Lake;

public sealed record LakeItem
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string Key { get; init; } = "";
    public string Source { get; init; } = "";
    public string Kind { get; init; } = "";
    public string ProviderId { get; init; } = "";
    public int Version { get; init; }
    public string Url { get; init; } = "";
    public string ContentType { get; init; } = "";
    public DateTime HarvestedAt { get; init; }
    public DateTime CheckedAt { get; init; }
    public string Hash { get; init; } = "";
    public long Size { get; init; }
    public string Location { get; init; } = "";
}
