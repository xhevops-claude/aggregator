namespace Collector.Core.Messages;

public interface IMessagePublisher
{
    Task PublishAsync<T>(string name, T message, CancellationToken cancellationToken) where T : IMessage;
}
