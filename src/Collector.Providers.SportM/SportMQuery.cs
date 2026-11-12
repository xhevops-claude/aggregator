namespace Collector.Providers.SportM;

internal static class SportMQuery
{
    public static string Of(string url)
    {
        var question = url.IndexOf('?');
        if (question < 0) return "";

        var query = url[(question + 1)..];
        var hash = query.IndexOf('#');
        return hash < 0 ? query : query[..hash];
    }

    public static string? Get(string query, string key)
    {
        foreach (var part in Parts(query))
        {
            if (!HasKey(part, key)) continue;

            var equals = part.IndexOf('=');
            var value = equals < 0 ? "" : part[(equals + 1)..];
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        return null;
    }

    public static IReadOnlyList<string> Keys(string query)
    {
        return Parts(query).Select(KeyOf).ToList();
    }

    public static string Without(string query, string key)
    {
        var kept = Parts(query).Where(part => !HasKey(part, key));
        return string.Join('&', kept);
    }

    public static string With(string query, string key, string value)
    {
        var rest = Without(query, key);
        var pair = $"{key}={Uri.EscapeDataString(value)}";
        return rest.Length == 0 ? pair : $"{rest}&{pair}";
    }

    public static string Normalize(string listing)
    {
        return Without(listing, "Page");
    }

    private static IEnumerable<string> Parts(string query)
    {
        var trimmed = query.Trim().TrimStart('?');
        return trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool HasKey(string part, string key)
    {
        return string.Equals(KeyOf(part), key, StringComparison.OrdinalIgnoreCase);
    }

    private static string KeyOf(string part)
    {
        var equals = part.IndexOf('=');
        var name = equals < 0 ? part : part[..equals];
        return name.Trim();
    }
}
