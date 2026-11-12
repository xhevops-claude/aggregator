namespace Collector.Harvesting;

internal sealed record RobotsRule(bool Allow, string Pattern)
{
    public bool Matches(string path)
    {
        var anchored = Pattern.EndsWith('$');
        var body = anchored ? Pattern[..^1] : Pattern;
        var parts = body.Split('*');
        if (!path.StartsWith(parts[0], StringComparison.Ordinal)) return false;

        var position = parts[0].Length;
        if (parts.Length == 1) return !anchored || path.Length == position;

        for (var i = 1; i < parts.Length - 1; i++)
        {
            var found = path.IndexOf(parts[i], position, StringComparison.Ordinal);
            if (found < 0) return false;
            position = found + parts[i].Length;
        }

        var last = parts[^1];
        if (anchored) return path.Length - last.Length >= position && path.EndsWith(last, StringComparison.Ordinal);
        return path.IndexOf(last, position, StringComparison.Ordinal) >= 0;
    }
}
