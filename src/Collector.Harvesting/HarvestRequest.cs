using Collector.Core.Messages;
using Collector.Core.Platform;

namespace Collector.Harvesting;

public sealed record HarvestRequest
{
    public Guid ProjectId { get; init; }
    public string Namespace { get; init; } = "";
    public required Source Source { get; init; }
    public List<HarvestTarget> Targets { get; init; } = [];
    public Dictionary<string, string> Parameters { get; init; } = [];
    public int? Limit { get; init; }

    public string? Parameter(string name) => Parameters.TryGetValue(name, out var value) ? value : null;
}
