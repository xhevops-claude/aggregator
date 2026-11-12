namespace Collector.Harvesting;

public sealed class HostBudget
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    private readonly TimeProvider time;
    private readonly Lock gate = new();
    private readonly Dictionary<string, List<DateTimeOffset>> hosts = new(StringComparer.OrdinalIgnoreCase);

    public HostBudget(TimeProvider time)
    {
        this.time = time;
    }

    public DateTimeOffset Reserve(Uri url, int maxPerHour, DateTimeOffset notBefore)
    {
        lock (gate)
        {
            var starts = StartsOf(url);
            var now = time.GetUtcNow();
            starts.RemoveAll(start => start + Hour <= now);

            var slot = Latest(now, notBefore);
            if (starts.Count > 0) slot = Latest(slot, starts[^1]);
            if (starts.Count >= maxPerHour) slot = Latest(slot, starts[^maxPerHour] + Hour);
            starts.Add(slot);
            return slot;
        }
    }

    private List<DateTimeOffset> StartsOf(Uri url)
    {
        if (hosts.TryGetValue(url.Authority, out var starts)) return starts;

        starts = [];
        hosts[url.Authority] = starts;
        return starts;
    }

    private static DateTimeOffset Latest(DateTimeOffset first, DateTimeOffset second)
    {
        return first > second ? first : second;
    }
}
