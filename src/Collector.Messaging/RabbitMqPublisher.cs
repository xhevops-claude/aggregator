using Collector.Core.Messages;
using RabbitMQ.Client;

namespace Collector.Messaging;

public sealed class RabbitMqPublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly IChannel channel;
    private readonly string exchange;

    private RabbitMqPublisher(IChannel channel, string exchange)
    {
        this.channel = channel;
        this.exchange = exchange;
    }

    public static async Task<RabbitMqPublisher> CreateAsync(
        IConnection connection, RabbitMqOptions options, CancellationToken cancellationToken)
    {
        var channel = await connection.CreateChannelAsync(ChannelOptions.Confirmed, cancellationToken);
        return new RabbitMqPublisher(channel, TopologyNames.Exchange(options.Prefix));
    }

    public Task PublishAsync<T>(string name, T message, CancellationToken cancellationToken) where T : IMessage
    {
        return PublishAsync(name, message, 0, cancellationToken);
    }

    public async Task PublishAsync<T>(string name, T message, byte priority, CancellationToken cancellationToken)
        where T : IMessage
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(message);
        if (message.ProjectId == Guid.Empty)
        {
            throw new ArgumentException("Every message carries a projectId.", nameof(message));
        }

        var properties = new BasicProperties
        {
            ContentType = MessageJson.ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            Type = name,
            MessageId = Guid.NewGuid().ToString(),
            Priority = priority,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = MessageHeaders.For(message),
        };
        var body = MessageJson.Serialize(message);

        await channel.BasicPublishAsync(exchange, name, mandatory: true, properties, body, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (channel.IsOpen)
        {
            await channel.CloseAsync();
        }

        await channel.DisposeAsync();
    }
}
