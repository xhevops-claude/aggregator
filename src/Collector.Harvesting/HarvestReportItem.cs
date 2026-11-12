namespace Collector.Harvesting;

public sealed record HarvestReportItem
{
    public string Kind { get; init; } = "";
    public string ProviderId { get; init; } = "";
    public string Key { get; init; } = "";
    public int Version { get; init; }
}
