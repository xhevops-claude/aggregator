using System.Text;

namespace Collector.Harvesting;

public sealed class RobotsRules
{
    private readonly List<RobotsRule> rules;

    private RobotsRules(List<RobotsRule> rules)
    {
        this.rules = rules;
    }

    public static RobotsRules AllowAll { get; } = new([]);

    public int Count => rules.Count;

    public static RobotsRules Parse(string text)
    {
        var rules = new List<RobotsRule>();
        var forEveryone = false;
        var readingAgents = false;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = WithoutComment(rawLine).TrimStart('\uFEFF').Trim();
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;

            var field = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            if (field == "user-agent")
            {
                if (!readingAgents) forEveryone = false;
                readingAgents = true;
                if (value == "*") forEveryone = true;
                continue;
            }

            if (field == "sitemap") continue;

            readingAgents = false;
            if (!forEveryone || value.Length == 0) continue;
            if (field == "allow") rules.Add(new RobotsRule(true, Encode(value)));
            if (field == "disallow") rules.Add(new RobotsRule(false, Encode(value)));
        }

        return new RobotsRules(rules);
    }

    public bool IsAllowed(Uri url)
    {
        var path = url.PathAndQuery;
        if (path == "/robots.txt") return true;

        RobotsRule? winner = null;
        foreach (var rule in rules)
        {
            if (!rule.Matches(path)) continue;
            if (winner is null || Beats(rule, winner)) winner = rule;
        }

        return winner is null || winner.Allow;
    }

    private static bool Beats(RobotsRule rule, RobotsRule current)
    {
        if (rule.Pattern.Length != current.Pattern.Length) return rule.Pattern.Length > current.Pattern.Length;
        return rule.Allow;
    }

    private static string WithoutComment(string line)
    {
        var hash = line.IndexOf('#');
        return hash < 0 ? line : line[..hash];
    }

    private static string Encode(string pattern)
    {
        if (pattern.All(char.IsAscii)) return pattern;

        var builder = new StringBuilder();
        foreach (var rune in pattern.EnumerateRunes())
        {
            var text = rune.ToString();
            builder.Append(rune.IsAscii ? text : Uri.EscapeDataString(text));
        }

        return builder.ToString();
    }
}
