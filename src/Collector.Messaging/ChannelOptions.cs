using RabbitMQ.Client;

namespace Collector.Messaging;

internal static class ChannelOptions
{
    public static CreateChannelOptions Confirmed { get; } = new(
        publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
}
