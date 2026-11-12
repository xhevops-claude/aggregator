namespace Collector.Providers.Nbshop;

public sealed record NbshopTile
{
    public string ProviderId { get; init; } = "";
    public string Category { get; init; } = "";
    public string Url { get; init; } = "";
    public string Title { get; init; } = "";
}
