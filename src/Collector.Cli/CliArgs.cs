namespace Collector.Cli;

public sealed class CliArgs
{
    private readonly Dictionary<string, List<string>> options = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> flags = new(StringComparer.OrdinalIgnoreCase);

    public CliArgs(IEnumerable<string> args)
    {
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var arg = list[i];
            if (!arg.StartsWith("--"))
            {
                Positional.Add(arg);
                continue;
            }

            var name = arg[2..];
            var hasValue = i + 1 < list.Count && !list[i + 1].StartsWith("--");
            if (!hasValue)
            {
                flags.Add(name);
                continue;
            }

            if (!options.TryGetValue(name, out var values)) options[name] = values = [];
            values.Add(list[++i]);
        }
    }

    public List<string> Positional { get; } = [];

    public string? Option(string name) => options.TryGetValue(name, out var values) ? values[^1] : null;

    public IReadOnlyList<string> Options(string name) => options.TryGetValue(name, out var values) ? values : [];

    public bool Flag(string name) => flags.Contains(name);

    public int? Int(string name)
    {
        var value = Option(name);
        if (value is null) return null;
        if (int.TryParse(value, out var number) && number > 0) return number;
        throw new CliException($"--{name} must be a positive whole number, got '{value}'");
    }

    public string At(int index, string what)
    {
        if (index < Positional.Count) return Positional[index];
        throw new CliException($"missing {what}");
    }
}
