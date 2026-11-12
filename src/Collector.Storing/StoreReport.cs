namespace Collector.Storing;

public sealed record StoreReport
{
    public int Applied { get; init; }
    public int Rejected { get; init; }
    public int Failed { get; init; }
    public List<ApplyResult> Results { get; init; } = [];
}
