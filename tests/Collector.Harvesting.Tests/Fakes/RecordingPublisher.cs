using Collector.Core.Messages;

namespace Collector.Harvesting.Tests.Fakes;

internal sealed class RecordingPublisher : IMessagePublisher
{
    private readonly Lock gate = new();

    public List<PublishedMessage> Published { get; } = [];

    public List<string> Names => Published.Select(message => message.Name).ToList();

    public Task PublishAsync<T>(string name, T message, CancellationToken cancellationToken) where T : IMessage
    {
        lock (gate) Published.Add(new PublishedMessage(name, message));
        return Task.CompletedTask;
    }

    public List<T> Of<T>(string name)
    {
        return Published.Where(message => message.Name == name).Select(message => (T)message.Message).ToList();
    }
}
