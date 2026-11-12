namespace Collector.Core.Messages;

public sealed record RunRequested : IMessage
{
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public Guid ItemId { get; init; }
    public string Namespace { get; init; } = "";
    public string Job { get; init; } = "";
    public string Strategy { get; init; } = "";
    public Dictionary<string, string> Parameters { get; init; } = [];
    public int? Limit { get; init; }
    public string RequestedBy { get; init; } = "";
}
