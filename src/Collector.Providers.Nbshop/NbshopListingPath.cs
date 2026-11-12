using System.Text.RegularExpressions;

namespace Collector.Providers.Nbshop;

public sealed partial record NbshopListingPath
{
    public string Path { get; init; } = "";

    public static NbshopListingPath? Parse(string? listing)
    {
        var path = listing?.Trim().Trim('/') ?? "";
        return path.Length == 0 ? null : new NbshopListingPath { Path = path };
    }

    public static NbshopListingPath? ForBrand(string? brand)
    {
        var slug = BrandSlug(brand);
        return slug.Length == 0 ? null : new NbshopListingPath { Path = $"proizvodi/{slug}" };
    }

    public static string BrandSlug(string? brand)
    {
        if (string.IsNullOrWhiteSpace(brand)) return "";

        var lower = brand.ToLowerInvariant();
        return NotSlugCharacters().Replace(lower, "-").Trim('-');
    }

    public string ProviderId(int page) => $"{Path}/page-{page}";

    public string PageUrl(string baseUrl, string language, int page)
    {
        var first = $"{baseUrl.TrimEnd('/')}/{language}/{Path}";
        return page == 1 ? first : $"{first}/page-{page}";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotSlugCharacters();
}
