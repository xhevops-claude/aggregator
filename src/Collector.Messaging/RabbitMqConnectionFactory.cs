using RabbitMQ.Client;

namespace Collector.Messaging;

public static class RabbitMqConnectionFactory
{
    public static Task<IConnection> CreateAsync(
        RabbitMqOptions options, string clientName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var factory = new ConnectionFactory { Uri = options.Uri, ClientProvidedName = clientName };
        return factory.CreateConnectionAsync(cancellationToken);
    }
}
