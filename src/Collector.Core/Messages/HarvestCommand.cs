using Collector.Core.Platform;

namespace Collector.Core.Messages;

public sealed record HarvestCommand : IMessage
{
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public Guid ItemId { get; init; }
    public string Namespace { get; init; } = "";
    public string Command { get; init; } = "";
    public string Source { get; init; } = "";
    public List<HarvestTarget> Targets { get; init; } = [];
    public Dictionary<string, string> Parameters { get; init; } = [];
    public int? Limit { get; init; }
    public Mechanism Mechanism { get; init; }
    public Pacing Pacing { get; init; } = new();
}
