using System.Globalization;
using Collector.Core.Messages;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Collector.Messaging;

public sealed class MessageConsumer<T> : IAsyncDisposable where T : IMessage
{
    private readonly IConnection connection;
    private readonly ConsumerOptions options;
    private readonly Func<T, CancellationToken, Task> handler;
    private readonly string queue;
    private readonly string retryQueue;
    private readonly ushort prefetch;
    private readonly CancellationTokenSource stopping = new();
    private IChannel? channel;
    private IChannel? retryChannel;
    private string? consumerTag;

    public MessageConsumer(IConnection connection, RabbitMqOptions rabbit, ConsumerOptions options,
        Func<T, CancellationToken, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Queue);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxAttempts, 1);
        var resolvedPrefetch = options.PrefetchOrTopologyDefault();
        ArgumentOutOfRangeException.ThrowIfZero(resolvedPrefetch);

        this.connection = connection;
        this.options = options;
        this.handler = handler;
        prefetch = resolvedPrefetch;
        queue = TopologyNames.Queue(rabbit.Prefix, options.Queue);
        retryQueue = TopologyNames.RetryQueue(rabbit.Prefix, options.Queue);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (channel is not null || stopping.IsCancellationRequested)
        {
            throw new InvalidOperationException("A consumer starts once.");
        }

        retryChannel = await connection.CreateChannelAsync(ChannelOptions.Confirmed, cancellationToken);
        var consuming = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        channel = consuming;
        await consuming.BasicQosAsync(0, prefetch, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(consuming);
        consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(consuming, delivery);
        consumerTag = await consuming.BasicConsumeAsync(queue, autoAck: false, consumer, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (channel is not null && consumerTag is not null && channel.IsOpen)
        {
            await channel.BasicCancelAsync(consumerTag, cancellationToken: cancellationToken);
        }

        consumerTag = null;
        await stopping.CancelAsync();
        await CloseAsync(channel, cancellationToken);
        await CloseAsync(retryChannel, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        if (channel is not null)
        {
            await channel.DisposeAsync();
        }

        if (retryChannel is not null)
        {
            await retryChannel.DisposeAsync();
        }

        channel = null;
        retryChannel = null;
        stopping.Dispose();
    }

    private async Task OnReceivedAsync(IChannel consuming, BasicDeliverEventArgs delivery)
    {
        if (!MessageJson.TryDeserialize<T>(delivery.Body, out var message))
        {
            await consuming.BasicRejectAsync(delivery.DeliveryTag, requeue: false, CancellationToken.None);
            return;
        }

        var attempt = MessageHeaders.ReadAttempt(delivery.BasicProperties.Headers);
        bool handled;
        try
        {
            await handler(message, stopping.Token);
            handled = true;
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            handled = false;
        }

        if (handled)
        {
            await consuming.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
            return;
        }

        if (attempt < options.MaxAttempts && await TryScheduleRetryAsync(delivery, attempt))
        {
            await consuming.BasicAckAsync(delivery.DeliveryTag, multiple: false, CancellationToken.None);
            return;
        }

        await consuming.BasicRejectAsync(delivery.DeliveryTag, requeue: false, CancellationToken.None);
    }

    private async Task<bool> TryScheduleRetryAsync(BasicDeliverEventArgs delivery, int attempt)
    {
        var publishing = retryChannel;
        if (publishing is null)
        {
            return false;
        }

        var properties = RetryProperties(delivery.BasicProperties, attempt + 1, options.RetryDelayAfter(attempt));
        var body = delivery.Body;
        try
        {
            await publishing.BasicPublishAsync("", retryQueue, mandatory: true, properties, body);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static BasicProperties RetryProperties(IReadOnlyBasicProperties original, int attempt, TimeSpan delay)
    {
        var headers = new Dictionary<string, object?>();
        foreach (var (key, value) in original.Headers ?? new Dictionary<string, object?>())
        {
            if (!key.StartsWith("x-", StringComparison.Ordinal))
            {
                headers[key] = value;
            }
        }

        headers[MessageHeaders.Attempt] = attempt;
        var expiration = ((long)delay.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        return new BasicProperties(original) { Expiration = expiration, Headers = headers };
    }

    private static async Task CloseAsync(IChannel? channel, CancellationToken cancellationToken)
    {
        if (channel is not null && channel.IsOpen)
        {
            await channel.CloseAsync(cancellationToken);
        }
    }
}
