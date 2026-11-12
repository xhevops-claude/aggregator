namespace Collector.Harvesting;

public sealed class HarvestCommandCatalog
{
    private readonly Dictionary<string, IHarvestCommand> byName = new(StringComparer.OrdinalIgnoreCase);

    public HarvestCommandCatalog(IEnumerable<IHarvestCommand> commands)
    {
        foreach (var command in commands)
        {
            if (string.IsNullOrWhiteSpace(command.Name))
            {
                throw new ArgumentException("A harvest command needs a name", nameof(commands));
            }

            if (!byName.TryAdd(command.Name, command))
            {
                throw new ArgumentException($"Harvest command '{command.Name}' is registered twice", nameof(commands));
            }
        }
    }

    public IReadOnlyCollection<string> Names => byName.Keys;

    public IHarvestCommand? Find(string name)
    {
        return byName.GetValueOrDefault(name);
    }

    public IHarvestCommand Get(string name)
    {
        var command = Find(name);
        if (command is null) throw new KeyNotFoundException($"No harvest command named '{name}'");
        return command;
    }
}
