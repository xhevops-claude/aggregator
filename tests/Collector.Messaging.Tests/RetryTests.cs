using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Collector.Core.Messages;
using RabbitMQ.Client;

namespace Collector.Messaging.Tests;

[Trait("Category", "Integration")]
public class RetryTests : RabbitTestBase
{
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Tolerance = TimeSpan.FromMilliseconds(5);

    [Fact]
    public async Task Failed_message_is_retried_with_doubling_delays_until_it_is_handled()
    {
        var clock = Stopwatch.StartNew();
        var calls = new ConcurrentQueue<TimeSpan>();
        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Handle(AdaptCommand message, CancellationToken token)
        {
            calls.Enqueue(clock.Elapsed);
            if (calls.Count < 3)
            {
                throw new InvalidOperationException("Not yet.");
            }

            handled.TrySetResult();
            return Task.CompletedTask;
        }

        await using var consumer = Consumer<AdaptCommand>(Retrying(maxAttempts: 5), Handle);
        await consumer.StartAsync(Cancellation);
        await using var publisher = await CreatePublisherAsync();

        await publisher.PublishAsync(MessageNames.AdaptCommand, TestMessages.AdaptCommand(), Cancellation);

        await handled.Task.WaitAsync(Cancellation);
        var times = calls.ToArray();
        Assert.Equal(3, times.Length);
        Assert.True(times[1] - times[0] >= FirstDelay - Tolerance, $"first gap {times[1] - times[0]}");
        Assert.True(times[2] - times[1] >= FirstDelay * 2 - Tolerance, $"second gap {times[2] - times[1]}");
        await WaitForCountAsync(QueueName(QueueNames.AdapterAdaptCommand), 0);
        Assert.Equal(0u, await CountAsync(RetryQueueName(QueueNames.AdapterAdaptCommand)));
        Assert.Equal(0u, await CountAsync(DeadQueueName(QueueNames.AdapterAdaptCommand)));
    }

    [Fact]
    public async Task Message_lands_in_the_dead_letter_queue_once_its_attempts_are_spent()
    {
        var command = TestMessages.AdaptCommand();
        var calls = 0;

        Task Handle(AdaptCommand message, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("Always fails.");
        }

        await using var consumer = Consumer<AdaptCommand>(Retrying(maxAttempts: 3), Handle);
        await consumer.StartAsync(Cancellation);
        await using var publisher = await CreatePublisherAsync();

        await publisher.PublishAsync(MessageNames.AdaptCommand, command, Cancellation);

        var dead = DeadQueueName(QueueNames.AdapterAdaptCommand);
        await WaitForCountAsync(dead, 1);
        await consumer.StopAsync(Cancellation);
        Assert.Equal(3, Volatile.Read(ref calls));
        Assert.Equal(0u, await CountAsync(QueueName(QueueNames.AdapterAdaptCommand)));
        Assert.Equal(0u, await CountAsync(RetryQueueName(QueueNames.AdapterAdaptCommand)));

        var result = await Channel.BasicGetAsync(dead, autoAck: true, Cancellation);
        Assert.NotNull(result);
        Assert.Equal(3, MessageHeaders.ReadAttempt(result.BasicProperties.Headers));
        Assert.Equal(MessageNames.AdaptCommand, result.BasicProperties.Type);
        Assert.True(MessageJson.TryDeserialize<AdaptCommand>(result.Body, out var back));
        Assert.Equal(command.ItemId, back.ItemId);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("null")]
    [InlineData("[1, 2]")]
    [InlineData("{}")]
    public async Task Malformed_message_goes_straight_to_the_dead_letter_queue(string body)
    {
        var calls = 0;

        Task Handle(AdaptCommand message, CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        }

        await using var consumer = Consumer<AdaptCommand>(Retrying(maxAttempts: 5), Handle);
        await consumer.StartAsync(Cancellation);
        var properties = new BasicProperties { ContentType = MessageJson.ContentType };
        var exchange = TopologyNames.Exchange(Options.Prefix);
        var bytes = Encoding.UTF8.GetBytes(body);

        await Channel.BasicPublishAsync(exchange, MessageNames.AdaptCommand, false, properties, bytes, Cancellation);

        await WaitForCountAsync(DeadQueueName(QueueNames.AdapterAdaptCommand), 1);
        Assert.Equal(0, Volatile.Read(ref calls));
        Assert.Equal(0u, await CountAsync(RetryQueueName(QueueNames.AdapterAdaptCommand)));
    }

    private static ConsumerOptions Retrying(int maxAttempts)
    {
        return new ConsumerOptions
        {
            Queue = QueueNames.AdapterAdaptCommand,
            MaxAttempts = maxAttempts,
            FirstRetryDelay = FirstDelay,
        };
    }
}
