using Collector.Core.Platform;

namespace Collector.Harvesting;

public sealed record HostSettings
{
    public TimeSpan MinRequestInterval { get; init; } = TimeSpan.FromSeconds(1);
    public bool RespectRobots { get; init; } = true;

    public static HostSettings From(Source source)
    {
        var interval = source.MinRequestInterval;
        return new HostSettings { MinRequestInterval = interval, RespectRobots = source.RespectRobots };
    }

    public static HostSettings Strictest(IEnumerable<HostSettings> all)
    {
        var interval = TimeSpan.Zero;
        var robots = false;
        foreach (var settings in all)
        {
            if (settings.MinRequestInterval > interval) interval = settings.MinRequestInterval;
            robots = robots || settings.RespectRobots;
        }

        return new HostSettings { MinRequestInterval = interval, RespectRobots = robots };
    }
}
