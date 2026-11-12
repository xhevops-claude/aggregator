using Collector.Core.Runs;

namespace Collector.Core.Messages;

public sealed record ItemCompleted : IMessage
{
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public Guid ItemId { get; init; }
    public RunItemState State { get; init; }
    public string? ErrorClass { get; init; }
    public string? ErrorText { get; init; }
}
