namespace Collector.Providers.SportM.Parsing;

internal sealed record CatalogTile
{
    public string ProductId { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Brand { get; init; }
    public string? Group { get; init; }
    public string? ImagePath { get; init; }
    public decimal? Price { get; init; }
    public decimal? OldPrice { get; init; }
}
