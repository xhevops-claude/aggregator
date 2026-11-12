using System.Collections.Concurrent;
using Collector.Core.Messages;

namespace Collector.Messaging.Tests;

[Trait("Category", "Integration")]
public class PriorityTests : RabbitTestBase
{
    [Fact]
    public async Task Harvest_commands_are_delivered_highest_priority_first()
    {
        byte[] priorities = [1, 9, 5, 0, 7];
        await using var publisher = await CreatePublisherAsync();
        foreach (var priority in priorities)
        {
            var command = TestMessages.HarvestCommand($"priority-{priority}");
            await publisher.PublishAsync(MessageNames.HarvestCommand, command, priority, Cancellation);
        }

        await WaitForCountAsync(QueueName(QueueNames.HarvesterHarvestCommand), (uint)priorities.Length);

        var received = new ConcurrentQueue<string>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Handle(HarvestCommand message, CancellationToken token)
        {
            received.Enqueue(message.Command);
            if (received.Count == priorities.Length)
            {
                done.TrySetResult();
            }

            return Task.CompletedTask;
        }

        var options = new ConsumerOptions { Queue = QueueNames.HarvesterHarvestCommand };
        await using var consumer = Consumer<HarvestCommand>(options, Handle);
        await consumer.StartAsync(Cancellation);
        await done.Task.WaitAsync(Cancellation);

        string[] expected = ["priority-9", "priority-7", "priority-5", "priority-1", "priority-0"];
        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task Live_check_overtakes_the_backlog_of_a_busy_harvester()
    {
        const int backlog = 20;
        const string liveCheck = "live-check";
        await using var publisher = await CreatePublisherAsync();
        for (var index = 0; index < backlog; index++)
        {
            var bulk = TestMessages.HarvestCommand($"bulk-{index}");
            await publisher.PublishAsync(MessageNames.HarvestCommand, bulk, 0, Cancellation);
        }

        await WaitForCountAsync(QueueName(QueueNames.HarvesterHarvestCommand), backlog);

        var received = new ConcurrentQueue<string>();
        var busy = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task Handle(HarvestCommand message, CancellationToken token)
        {
            received.Enqueue(message.Command);
            busy.TrySetResult();
            await release.Task.WaitAsync(token);
            if (received.Count == backlog + 1)
            {
                done.TrySetResult();
            }
        }

        var options = new ConsumerOptions { Queue = QueueNames.HarvesterHarvestCommand };
        await using var consumer = Consumer<HarvestCommand>(options, Handle);
        await consumer.StartAsync(Cancellation);
        await busy.Task.WaitAsync(Cancellation);

        var live = TestMessages.HarvestCommand(liveCheck);
        await publisher.PublishAsync(MessageNames.HarvestCommand, live, 9, Cancellation);
        release.TrySetResult();
        await done.Task.WaitAsync(Cancellation);

        var order = received.ToList();
        Assert.Equal(backlog + 1, order.Count);
        Assert.Equal(1, order.IndexOf(liveCheck));
    }
}
