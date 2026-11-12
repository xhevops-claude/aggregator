namespace Collector.Lake;

public sealed record LakeWrite
{
    public Guid ProjectId { get; init; }
    public string Source { get; init; } = "";
    public string Kind { get; init; } = "";
    public string ProviderId { get; init; } = "";
    public string Url { get; init; } = "";
    public string ContentType { get; init; } = "";
    public DateTime HarvestedAt { get; init; }
    public byte[] Body { get; init; } = [];
}
