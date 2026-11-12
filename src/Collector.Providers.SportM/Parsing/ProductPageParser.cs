using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Collector.Providers.SportM.Parsing;

internal static partial class ProductPageParser
{
    private const string HeadingSuffix = " description";
    private const string ImagePrefix = "/ProductImages/";

    public static string? HeadingId(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        return HeadingId(document);
    }

    public static ProductPage? Parse(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var productId = HeadingId(document);
        if (productId is null) return null;

        var heading = document.QuerySelector("h2.details_description");
        return new ProductPage
        {
            ProductId = productId,
            Name = SportMText.Clean(heading?.TextContent),
            Price = SportMPrice.Parse(document.GetElementById(productId + " Price")?.TextContent),
            OldPrice = SportMPrice.Parse(document.GetElementById("old_price_pd")?.TextContent),
            Crumbs = ReadCrumbs(document),
            Sizes = ReadSizes(document),
            ImagePaths = ReadImages(document, html),
        };
    }

    private static string? HeadingId(IDocument document)
    {
        var heading = document.QuerySelector("h2.details_description");
        var id = heading?.Id?.Trim();
        if (string.IsNullOrEmpty(id)) return null;

        if (id.EndsWith(HeadingSuffix, StringComparison.OrdinalIgnoreCase)) id = id[..^HeadingSuffix.Length].Trim();
        return id.Length == 0 ? null : id;
    }

    private static List<Crumb> ReadCrumbs(IDocument document)
    {
        var crumbs = new List<Crumb>();
        foreach (var link in document.QuerySelectorAll(".breadcrumb-link"))
        {
            var text = SportMText.Clean(link.TextContent);
            if (text.Length > 0) crumbs.Add(new Crumb(text, link.GetAttribute("href") ?? ""));
        }

        return crumbs;
    }

    private static List<string> ReadSizes(IDocument document)
    {
        var sizes = new List<string>();
        foreach (var option in document.QuerySelectorAll("li.size-option"))
        {
            var label = SportMText.Clean(option.QuerySelector("div")?.TextContent);
            if (label.Length == 0) label = SportMText.Clean(option.TextContent);
            if (label.Length > 0) sizes.Add(label);
        }

        return sizes;
    }

    private static List<string> ReadImages(IDocument document, string html)
    {
        var images = new List<string>();
        var main = document.GetElementById("ImageDetails")?.GetAttribute("src");
        if (!string.IsNullOrWhiteSpace(main)) images.Add(main.Trim());

        foreach (Match match in GalleryEntry().Matches(html))
        {
            var path = match.Groups[1].Value.Trim();
            if (path.StartsWith(ImagePrefix, StringComparison.OrdinalIgnoreCase)) images.Add(path);
        }

        return images.Distinct(StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex(@"'s'\s*:\s*'([^']+)'")]
    private static partial Regex GalleryEntry();
}
