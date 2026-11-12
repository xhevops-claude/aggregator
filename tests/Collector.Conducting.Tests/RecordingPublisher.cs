using Collector.Core.Messages;

namespace Collector.Conducting.Tests;

internal sealed class RecordingPublisher : IMessagePublisher
{
    public List<(string Name, IMessage Message)> Published { get; } = [];

    public Task PublishAsync<T>(string name, T message, CancellationToken cancellationToken) where T : IMessage
    {
        Published.Add((name, message));
        return Task.CompletedTask;
    }

    public List<T> Of<T>(string name)
    {
        var named = Published.Where(sent => sent.Name == name);
        return named.Select(sent => sent.Message).OfType<T>().ToList();
    }
}
