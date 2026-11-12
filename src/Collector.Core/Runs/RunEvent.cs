namespace Collector.Core.Runs;

public sealed record RunEvent
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public DateTime At { get; init; }
    public string Type { get; init; } = "";
    public string? Detail { get; init; }
    public Decision? Decision { get; init; }
    public string? By { get; init; }
}
