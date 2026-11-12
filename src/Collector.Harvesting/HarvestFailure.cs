namespace Collector.Harvesting;

public sealed record HarvestFailure
{
    public string Kind { get; init; } = "";
    public string ProviderId { get; init; } = "";
    public string ErrorClass { get; init; } = "";
    public string ErrorText { get; init; } = "";
}
