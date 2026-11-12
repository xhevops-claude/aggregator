namespace Collector.Providers.Nbshop;

public static class NbshopShoeCategories
{
    public static IReadOnlySet<string> Slugs { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "patiki",
        "patiki-za-voda",
        "kopacki",
        "cizmi",
        "konduri",
        "sandali",
        "apostolki",
        "papuci",
        "baletanki",
    };

    public static bool Contains(string? slug) => slug is not null && Slugs.Contains(slug);
}
