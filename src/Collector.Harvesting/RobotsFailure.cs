namespace Collector.Harvesting;

internal sealed record RobotsFailure(string Reason, DateTimeOffset Until);
