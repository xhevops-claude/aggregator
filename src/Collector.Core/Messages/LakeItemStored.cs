namespace Collector.Core.Messages;

public sealed record LakeItemStored : IMessage
{
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public Guid ItemId { get; init; }
    public string Key { get; init; } = "";
    public int Version { get; init; }
    public string Hash { get; init; } = "";
    public long Size { get; init; }
}
