namespace Collector.Core.Entities;

public sealed record Listing : Entity
{
    public Guid? ProductId { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public decimal? Price { get; init; }
    public decimal? OldPrice { get; init; }
    public string? Currency { get; init; }
    public Availability Availability { get; init; }
    public List<Size> Sizes { get; init; } = [];
    public string Url { get; init; } = "";
    public TimeSpan? RefreshInterval { get; init; }
    public DateTime? InfoCheckedAt { get; init; }
    public DateTime? StockCheckedAt { get; init; }
}
