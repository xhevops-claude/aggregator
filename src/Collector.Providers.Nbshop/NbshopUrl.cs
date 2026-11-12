using System.Diagnostics.CodeAnalysis;

namespace Collector.Providers.Nbshop;

public static class NbshopUrl
{
    public static bool TryHttp(string? text, [NotNullWhen(true)] out Uri? uri)
    {
        if (Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var parsed) && IsHttp(parsed))
        {
            uri = parsed;
            return true;
        }

        uri = null;
        return false;
    }

    public static string Resolve(string baseUrl, string url)
    {
        var trimmed = url.Trim();
        if (TryHttp(trimmed, out var absolute)) return absolute.ToString();

        return $"{baseUrl.TrimEnd('/')}/{trimmed.TrimStart('/')}";
    }

    private static bool IsHttp(Uri uri) => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}
