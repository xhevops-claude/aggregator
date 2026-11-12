namespace Collector.Core.Messages;

public sealed record EntityAdapted : IMessage
{
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public Guid ItemId { get; init; }
    public Guid StagingId { get; init; }
}
