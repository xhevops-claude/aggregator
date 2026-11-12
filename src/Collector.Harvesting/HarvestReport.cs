namespace Collector.Harvesting;

public sealed record HarvestReport
{
    public List<HarvestReportItem> Stored { get; init; } = [];
    public List<HarvestReportItem> Unchanged { get; init; } = [];
    public List<HarvestFailure> Failed { get; init; } = [];
    public string? Error { get; init; }

    public bool Succeeded => Error is null && Failed.Count == 0;
    public int Total => Stored.Count + Unchanged.Count + Failed.Count;
}
