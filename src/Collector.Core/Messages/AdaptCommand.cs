namespace Collector.Core.Messages;

public sealed record AdaptCommand : IMessage
{
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public Guid ItemId { get; init; }
    public string Namespace { get; init; } = "";
    public string Command { get; init; } = "";
    public List<string> LakeKeys { get; init; } = [];
}
