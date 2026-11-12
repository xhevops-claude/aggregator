namespace Collector.Core.Messages;

public sealed record HarvestTarget
{
    public string ProviderId { get; init; } = "";
    public string? Url { get; init; }
}
