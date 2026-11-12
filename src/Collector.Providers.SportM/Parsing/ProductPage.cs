namespace Collector.Providers.SportM.Parsing;

internal sealed record ProductPage
{
    public string ProductId { get; init; } = "";
    public string Name { get; init; } = "";
    public decimal? Price { get; init; }
    public decimal? OldPrice { get; init; }
    public List<Crumb> Crumbs { get; init; } = [];
    public List<string> Sizes { get; init; } = [];
    public List<string> ImagePaths { get; init; } = [];
}
