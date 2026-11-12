using Collector.Core.Messages;
using RabbitMQ.Client;

namespace Collector.Messaging.Tests;

public abstract class RabbitTestBase : IAsyncLifetime
{
    private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));

    protected RabbitMqOptions Options { get; } = RabbitMqOptions.FromEnvironment() with { Prefix = NewPrefix() };
    protected IConnection Connection { get; private set; } = null!;
    protected IChannel Channel { get; private set; } = null!;
    protected CancellationToken Cancellation => timeout.Token;

    public async Task InitializeAsync()
    {
        Connection = await RabbitMqConnectionFactory.CreateAsync(Options, "collector-messaging-tests", Cancellation);
        Channel = await Connection.CreateChannelAsync(cancellationToken: Cancellation);
        await TopologySetup.EnsureAsync(Channel, Options.Prefix, Cancellation);
    }

    public async Task DisposeAsync()
    {
        await using (var cleanup = await Connection.CreateChannelAsync())
        {
            foreach (var queue in TopologySetup.Queues)
            {
                await cleanup.QueueDeleteAsync(TopologyNames.Queue(Options.Prefix, queue.Name));
                await cleanup.QueueDeleteAsync(TopologyNames.RetryQueue(Options.Prefix, queue.Name));
                await cleanup.QueueDeleteAsync(TopologyNames.DeadQueue(Options.Prefix, queue.Name));
            }

            await cleanup.ExchangeDeleteAsync(TopologyNames.Exchange(Options.Prefix));
            await cleanup.ExchangeDeleteAsync(TopologyNames.DeadExchange(Options.Prefix));
            await cleanup.CloseAsync();
        }

        await Channel.DisposeAsync();
        await Connection.CloseAsync();
        await Connection.DisposeAsync();
        timeout.Dispose();
    }

    protected string QueueName(string queue) => TopologyNames.Queue(Options.Prefix, queue);

    protected string RetryQueueName(string queue) => TopologyNames.RetryQueue(Options.Prefix, queue);

    protected string DeadQueueName(string queue) => TopologyNames.DeadQueue(Options.Prefix, queue);

    protected Task<uint> CountAsync(string queueName) => Channel.MessageCountAsync(queueName, Cancellation);

    protected Task<RabbitMqPublisher> CreatePublisherAsync()
    {
        return RabbitMqPublisher.CreateAsync(Connection, Options, Cancellation);
    }

    protected MessageConsumer<T> Consumer<T>(ConsumerOptions options, Func<T, CancellationToken, Task> handler)
        where T : IMessage
    {
        return new MessageConsumer<T>(Connection, Options, options, handler);
    }

    protected async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        while (!await condition())
        {
            await Task.Delay(25, Cancellation);
        }
    }

    protected async Task WaitForCountAsync(string queueName, uint expected)
    {
        await WaitUntilAsync(async () => await CountAsync(queueName) == expected);
    }

    private static string NewPrefix() => $"test.messaging.{Guid.NewGuid():N}";
}
