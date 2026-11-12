namespace Collector.Core.Entities;

public sealed record Size
{
    public string Label { get; init; } = "";
    public string? System { get; init; }
    public string? Ean { get; init; }
    public bool? InStock { get; init; }
    public string? Branch { get; init; }
}
