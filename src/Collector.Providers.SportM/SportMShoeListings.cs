using Collector.Core.Platform;

namespace Collector.Providers.SportM;

internal static class SportMShoeListings
{
    public const string SettingKey = "shoeListings";
    public const string FootwearGroup = "01";

    private static readonly Dictionary<string, string[]> ByProvider = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sportm"] = ["Segment=03&Grupa=01", "Segment=04&Grupa=01", "Segment=05&Grupa=01"],
        ["intersport"] = ["Segment=28&Grupa=01", "Segment=29&Grupa=01", "Segment=30&Grupa=01"],
    };

    public static IReadOnlyList<string> For(Source source)
    {
        if (source.Settings.TryGetValue(SettingKey, out var configured))
        {
            return Split(configured);
        }

        if (ByProvider.TryGetValue(source.Provider, out var listings)) return listings;
        return [];
    }

    public static IReadOnlyList<string> Split(string listings)
    {
        var queries = listings.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return queries.Select(SportMQuery.Normalize).Where(query => query.Length > 0).Distinct().ToList();
    }
}
