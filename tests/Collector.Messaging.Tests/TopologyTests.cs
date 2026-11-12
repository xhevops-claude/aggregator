using Collector.Core.Messages;

namespace Collector.Messaging.Tests;

[Trait("Category", "Integration")]
public class TopologyTests : RabbitTestBase
{
    [Fact]
    public async Task Every_queue_has_its_retry_and_dead_letter_queue()
    {
        await Channel.ExchangeDeclarePassiveAsync(TopologyNames.Exchange(Options.Prefix), Cancellation);
        await Channel.ExchangeDeclarePassiveAsync(TopologyNames.DeadExchange(Options.Prefix), Cancellation);

        foreach (var queue in TopologySetup.Queues)
        {
            await Channel.QueueDeclarePassiveAsync(QueueName(queue.Name), Cancellation);
            await Channel.QueueDeclarePassiveAsync(RetryQueueName(queue.Name), Cancellation);
            await Channel.QueueDeclarePassiveAsync(DeadQueueName(queue.Name), Cancellation);
        }

        Assert.True(Channel.IsOpen);
    }

    [Fact]
    public async Task Ensure_twice_keeps_the_topology_and_the_waiting_messages()
    {
        await using var publisher = await CreatePublisherAsync();
        await publisher.PublishAsync(MessageNames.AdaptCommand, TestMessages.AdaptCommand(), Cancellation);

        await using var again = await Connection.CreateChannelAsync(cancellationToken: Cancellation);
        await TopologySetup.EnsureAsync(again, Options.Prefix, Cancellation);
        await TopologySetup.EnsureAsync(again, Options.Prefix, Cancellation);

        Assert.True(again.IsOpen);
        Assert.Equal(1u, await CountAsync(QueueName(QueueNames.AdapterAdaptCommand)));
    }

    [Fact]
    public async Task Lake_item_stored_reaches_the_conductor_and_the_adapter()
    {
        await using var publisher = await CreatePublisherAsync();

        await publisher.PublishAsync(MessageNames.LakeItemStored, TestMessages.LakeItemStored(), Cancellation);

        await WaitForCountAsync(QueueName(QueueNames.ConductorLakeItemStored), 1);
        await WaitForCountAsync(QueueName(QueueNames.AdapterLakeItemStored), 1);
        Assert.Equal(0u, await CountAsync(QueueName(QueueNames.AdapterAdaptCommand)));
    }
}
