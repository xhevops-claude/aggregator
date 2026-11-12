namespace Collector.Core.Messages;

public interface IMessage
{
    Guid ProjectId { get; }
    Guid RunId { get; }
    Guid ItemId { get; }
}
