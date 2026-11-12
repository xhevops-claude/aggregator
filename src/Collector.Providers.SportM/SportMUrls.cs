using System.Text;
using Collector.Core.Platform;

namespace Collector.Providers.SportM;

internal static class SportMUrls
{
    public const string ProductIdKey = "ProductID";
    private const string KeptPathCharacters = "/:_.-";

    public static string CatalogPage(Source source, string query, int page)
    {
        var pageQuery = query.Length == 0 ? $"Page={page}" : $"{query}&Page={page}";
        return $"{Base(source)}/ProductCatalog?{pageQuery}";
    }

    public static string ProductPage(Source source, string productId)
    {
        return $"{Base(source)}/ProductCatalog/ProductDetails?{ProductIdKey}={Uri.EscapeDataString(productId)}";
    }

    public static string WithoutContext(string productUrl)
    {
        var productId = SportMQuery.Get(SportMQuery.Of(productUrl), ProductIdKey)?.Trim();
        if (string.IsNullOrEmpty(productId)) return productUrl;

        var end = productUrl.IndexOfAny(['?', '#']);
        var address = end < 0 ? productUrl : productUrl[..end];
        return $"{address}?{ProductIdKey}={Uri.EscapeDataString(productId)}";
    }

    public static bool HasContext(string productUrl)
    {
        var keys = SportMQuery.Keys(SportMQuery.Of(productUrl));
        return keys.Any(key => !string.Equals(key, ProductIdKey, StringComparison.OrdinalIgnoreCase));
    }

    public static string Absolute(Source source, string path)
    {
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return path;
        if (path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return path;

        var relative = path.StartsWith('/') ? path : "/" + path;
        return Base(source) + EncodePath(relative);
    }

    public static string EncodePath(string path)
    {
        var encoded = new StringBuilder();
        Span<byte> bytes = stackalloc byte[4];
        foreach (var rune in path.EnumerateRunes())
        {
            if (rune.IsAscii && IsKept((char)rune.Value))
            {
                encoded.Append((char)rune.Value);
                continue;
            }

            var length = rune.EncodeToUtf8(bytes);
            foreach (var b in bytes[..length]) encoded.Append('%').Append(b.ToString("X2"));
        }

        return encoded.ToString();
    }

    private static bool IsKept(char c) => char.IsAsciiLetterOrDigit(c) || KeptPathCharacters.Contains(c);

    private static string Base(Source source) => source.BaseUrl.TrimEnd('/');
}
