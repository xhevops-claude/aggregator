using System.Text;

namespace Collector.Messaging;

public sealed record RabbitMqOptions
{
    public const string EnvironmentVariable = "COLLECTOR_RABBIT";
    public const string DefaultPrefix = "collector";

    public static Uri DefaultUri { get; } = new("amqp://collector:collector@localhost:5673/");

    public Uri Uri { get; init; } = DefaultUri;
    public string Prefix { get; init; } = DefaultPrefix;

    public static RabbitMqOptions FromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return new RabbitMqOptions();
        }

        return new RabbitMqOptions { Uri = new Uri(value) };
    }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Host = {Uri.Host}:{Uri.Port}, Prefix = {Prefix}");
        return true;
    }
}
