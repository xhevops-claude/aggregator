namespace Collector.Core.Platform;

public sealed record NodeSelection
{
    public int Count { get; init; } = 1;
    public string Geo { get; init; } = "any";
    public string? Node { get; init; }
}
