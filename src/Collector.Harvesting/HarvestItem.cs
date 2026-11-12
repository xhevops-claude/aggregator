namespace Collector.Harvesting;

public sealed record HarvestItem
{
    public string Kind { get; init; } = "";
    public string ProviderId { get; init; } = "";
    public string Url { get; init; } = "";
    public string ContentType { get; init; } = "";
    public byte[] Body { get; init; } = [];
    public DateTime HarvestedAt { get; init; }
    public string? ErrorClass { get; init; }
    public string? ErrorText { get; init; }

    public bool Failed => ErrorClass is not null;
}
