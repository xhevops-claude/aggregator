namespace Collector.Lake;

public sealed record LakeQuery
{
    public Guid ProjectId { get; init; }
    public string Source { get; init; } = "";
    public List<string> Kinds { get; init; } = [];
    public DateTime? HarvestedSince { get; init; }
    public int? Limit { get; init; }
}
