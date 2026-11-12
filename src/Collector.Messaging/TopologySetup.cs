using Collector.Core.Messages;
using RabbitMQ.Client;

namespace Collector.Messaging;

public static class TopologySetup
{
    public const byte HarvestPriorities = 10;
    public const ushort DefaultPrefetch = 16;
    public const ushort PriorityPrefetch = 1;

    public static IReadOnlyList<ConsumerQueue> Queues { get; } =
    [
        new(QueueNames.HarvesterHarvestCommand, MessageNames.HarvestCommand, PriorityPrefetch, HarvestPriorities),
        new(QueueNames.ConductorLakeItemStored, MessageNames.LakeItemStored, DefaultPrefetch),
        new(QueueNames.AdapterLakeItemStored, MessageNames.LakeItemStored, DefaultPrefetch),
        new(QueueNames.AdapterAdaptCommand, MessageNames.AdaptCommand, DefaultPrefetch),
        new(QueueNames.StoreWriterEntityAdapted, MessageNames.EntityAdapted, DefaultPrefetch),
        new(QueueNames.ConductorItemCompleted, MessageNames.ItemCompleted, DefaultPrefetch),
        new(QueueNames.ConductorRunRequested, MessageNames.RunRequested, DefaultPrefetch),
    ];

    public static ushort PrefetchFor(string queue)
    {
        var row = Queues.FirstOrDefault(candidate => candidate.Name == queue);
        if (row is null)
        {
            return DefaultPrefetch;
        }

        return row.Prefetch;
    }

    public static async Task EnsureAsync(IChannel channel, string prefix, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        await DeclareExchangeAsync(channel, TopologyNames.Exchange(prefix), ExchangeType.Topic, cancellationToken);
        await DeclareExchangeAsync(channel, TopologyNames.DeadExchange(prefix), ExchangeType.Direct, cancellationToken);

        foreach (var queue in Queues)
        {
            await EnsureQueueAsync(channel, prefix, queue, cancellationToken);
        }
    }

    private static async Task EnsureQueueAsync(
        IChannel channel, string prefix, ConsumerQueue queue, CancellationToken cancellationToken)
    {
        var exchange = TopologyNames.Exchange(prefix);
        var deadExchange = TopologyNames.DeadExchange(prefix);
        var name = TopologyNames.Queue(prefix, queue.Name);
        var dead = TopologyNames.DeadQueue(prefix, queue.Name);
        var retry = TopologyNames.RetryQueue(prefix, queue.Name);

        await DeclareQueueAsync(channel, dead, Classic(), cancellationToken);
        await channel.QueueBindAsync(dead, deadExchange, name, cancellationToken: cancellationToken);

        await DeclareQueueAsync(channel, retry, DeadLetterTo("", name), cancellationToken);

        var arguments = DeadLetterTo(deadExchange, name);
        if (queue.MaxPriority is { } maxPriority)
        {
            arguments["x-max-priority"] = (int)maxPriority;
        }

        await DeclareQueueAsync(channel, name, arguments, cancellationToken);
        await channel.QueueBindAsync(name, exchange, queue.Message, cancellationToken: cancellationToken);
    }

    private static Dictionary<string, object?> Classic()
    {
        return new Dictionary<string, object?> { ["x-queue-type"] = "classic" };
    }

    private static Dictionary<string, object?> DeadLetterTo(string exchange, string routingKey)
    {
        var arguments = Classic();
        arguments["x-dead-letter-exchange"] = exchange;
        arguments["x-dead-letter-routing-key"] = routingKey;
        return arguments;
    }

    private static Task DeclareExchangeAsync(
        IChannel channel, string name, string type, CancellationToken cancellationToken)
    {
        return channel.ExchangeDeclareAsync(name, type, durable: true, cancellationToken: cancellationToken);
    }

    private static async Task DeclareQueueAsync(
        IChannel channel, string name, Dictionary<string, object?> arguments, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(name, durable: true, exclusive: false, autoDelete: false, arguments,
            cancellationToken: cancellationToken);
    }
}
