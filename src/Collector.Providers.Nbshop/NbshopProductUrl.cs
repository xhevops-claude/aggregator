using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Collector.Providers.Nbshop;

public sealed partial record NbshopProductUrl
{
    public string Url { get; init; } = "";
    public string Language { get; init; } = "";
    public string Category { get; init; } = "";
    public string ProviderId { get; init; } = "";
    public string Slug { get; init; } = "";

    public string TitleFromSlug => Slug.Replace('-', ' ');

    public static bool TryParse(string? url, [NotNullWhen(true)] out NbshopProductUrl? result)
    {
        result = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        var match = ProductPath().Match(uri.AbsolutePath);
        if (!match.Success) return false;

        result = new NbshopProductUrl
        {
            Url = uri.ToString(),
            Language = match.Groups["language"].Value,
            Category = match.Groups["category"].Value,
            ProviderId = match.Groups["id"].Value,
            Slug = match.Groups["slug"].Value,
        };
        return true;
    }

    [GeneratedRegex(@"^/(?<language>[a-z]{2})/(?<category>[a-z0-9-]+)/(?<id>\d+)-(?<slug>[^/]+)/?$")]
    private static partial Regex ProductPath();
}
