using Collector.Core.Messages;
using Collector.Messaging;

namespace Collector.Workers;

public abstract class QueueWorker<T>(WorkerSettings settings, ILogger log) : BackgroundService where T : IMessage
{
    protected WorkerSettings Settings { get; } = settings;
    protected ILogger Log { get; } = log;

    protected abstract string Role { get; }
    protected abstract string Queue { get; }

    protected abstract Func<T, CancellationToken, Task> CreateHandler(IMessagePublisher publisher);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbit = Settings.Rabbit;
        var clientName = $"collector-{Role}-{Environment.MachineName}";
        await using var connection = await RabbitMqConnectionFactory.CreateAsync(rabbit, clientName, stoppingToken);

        await using (var setup = await connection.CreateChannelAsync(cancellationToken: stoppingToken))
        {
            await TopologySetup.EnsureAsync(setup, rabbit.Prefix, stoppingToken);
        }

        await using var publisher = await RabbitMqPublisher.CreateAsync(connection, rabbit, stoppingToken);
        var options = new ConsumerOptions { Queue = Queue };
        var handler = CreateHandler(publisher);
        await using var consumer = new MessageConsumer<T>(connection, rabbit, options, handler);

        await consumer.StartAsync(stoppingToken);
        Log.LogInformation("{Role} consuming {Queue}", Role, Queue);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        await consumer.StopAsync(CancellationToken.None);
    }
}
