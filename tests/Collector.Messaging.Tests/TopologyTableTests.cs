using Collector.Core.Messages;

namespace Collector.Messaging.Tests;

public class TopologyTableTests
{
    [Fact]
    public void Names_follow_the_prefix()
    {
        const string queue = QueueNames.HarvesterHarvestCommand;

        Assert.Equal("collector", TopologyNames.Exchange("collector"));
        Assert.Equal("collector.dead", TopologyNames.DeadExchange("collector"));
        Assert.Equal("collector.harvester.harvest-command", TopologyNames.Queue("collector", queue));
        Assert.Equal("collector.harvester.harvest-command.retry", TopologyNames.RetryQueue("collector", queue));
        Assert.Equal("collector.harvester.harvest-command.dead", TopologyNames.DeadQueue("collector", queue));
    }

    [Fact]
    public void Every_message_has_a_consumer_queue()
    {
        string[] messages =
        [
            MessageNames.HarvestCommand,
            MessageNames.LakeItemStored,
            MessageNames.AdaptCommand,
            MessageNames.EntityAdapted,
            MessageNames.ItemCompleted,
        ];

        var bound = TopologySetup.Queues.Select(queue => queue.Message).ToHashSet();

        Assert.All(messages, message => Assert.Contains(message, bound));
        Assert.Equal(2, TopologySetup.Queues.Count(queue => queue.Message == MessageNames.LakeItemStored));
    }

    [Fact]
    public void Only_harvest_commands_have_priorities()
    {
        var prioritised = Assert.Single(TopologySetup.Queues, queue => queue.MaxPriority is not null);

        Assert.Equal(QueueNames.HarvesterHarvestCommand, prioritised.Name);
        Assert.Equal((byte)10, prioritised.MaxPriority);
    }

    [Fact]
    public void Priority_queue_is_consumed_one_message_at_a_time()
    {
        var harvester = new ConsumerOptions { Queue = QueueNames.HarvesterHarvestCommand };
        var adapter = new ConsumerOptions { Queue = QueueNames.AdapterAdaptCommand };
        var unknown = new ConsumerOptions { Queue = "elsewhere.something" };

        Assert.Equal((ushort)1, harvester.PrefetchOrTopologyDefault());
        Assert.Equal(TopologySetup.DefaultPrefetch, adapter.PrefetchOrTopologyDefault());
        Assert.Equal(TopologySetup.DefaultPrefetch, unknown.PrefetchOrTopologyDefault());
    }

    [Fact]
    public void Explicit_prefetch_wins_over_the_topology_table()
    {
        var options = new ConsumerOptions { Queue = QueueNames.HarvesterHarvestCommand, Prefetch = 4 };

        Assert.Equal((ushort)4, options.PrefetchOrTopologyDefault());
    }
}
