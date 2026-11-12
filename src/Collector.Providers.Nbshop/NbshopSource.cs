using Collector.Core.Platform;

namespace Collector.Providers.Nbshop;

public static class NbshopSource
{
    public static string BaseUrl(Source source) => source.BaseUrl.TrimEnd('/');

    public static string Language(Source source)
    {
        var language = Setting(source, NbshopNames.LanguageSetting);
        return language ?? NbshopNames.DefaultLanguage;
    }

    public static string? SitemapRegion(Source source) => Setting(source, NbshopNames.SitemapRegionSetting);

    public static string? SitemapUrl(Source source)
    {
        var region = SitemapRegion(source);
        if (region is null) return null;

        return $"{BaseUrl(source)}/files/sitemap/{region}/product.xml";
    }

    private static string? Setting(Source source, string name)
    {
        if (!source.Settings.TryGetValue(name, out var value)) return null;

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
