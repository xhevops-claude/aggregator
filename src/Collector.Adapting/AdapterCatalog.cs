using System.Diagnostics.CodeAnalysis;

namespace Collector.Adapting;

public sealed class AdapterCatalog
{
    private readonly Dictionary<string, IAdapter> adapters = new(StringComparer.OrdinalIgnoreCase);

    public AdapterCatalog(IEnumerable<IAdapter> adapters)
    {
        foreach (var adapter in adapters)
        {
            var key = Key(adapter.Platform, adapter.Domain);
            if (!this.adapters.TryAdd(key, adapter))
            {
                var message = $"Adapters {this.adapters[key].Name} and {adapter.Name} both serve {key}";
                throw new ArgumentException(message, nameof(adapters));
            }
        }
    }

    public IReadOnlyCollection<IAdapter> All => adapters.Values;

    public bool TryGet(string platform, string domain, [NotNullWhen(true)] out IAdapter? adapter) =>
        adapters.TryGetValue(Key(platform, domain), out adapter);

    public IAdapter Get(string platform, string domain)
    {
        if (TryGet(platform, domain, out var adapter)) return adapter;

        throw new KeyNotFoundException($"No adapter serves {Key(platform, domain)}");
    }

    private static string Key(string platform, string domain) => $"{platform}/{domain}";
}
