using System.Collections.Concurrent;

namespace Collector.Harvesting;

internal sealed class HostSettingsTable
{
    private readonly HostSettings fallback;
    private readonly Dictionary<string, HostSettings> configured;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HostSettings>> registered;

    public HostSettingsTable(FetcherOptions options)
    {
        fallback = options.DefaultHost;
        configured = new Dictionary<string, HostSettings>(options.Hosts, StringComparer.OrdinalIgnoreCase);
        registered = new(StringComparer.OrdinalIgnoreCase);
    }

    public void Register(string host, string source, HostSettings settings)
    {
        var sources = registered.GetOrAdd(host, _ => new ConcurrentDictionary<string, HostSettings>());
        sources[source] = settings;
    }

    public HostSettings For(string host)
    {
        if (registered.TryGetValue(host, out var sources) && !sources.IsEmpty)
        {
            return HostSettings.Strictest(sources.Values);
        }

        return configured.GetValueOrDefault(host, fallback);
    }
}
