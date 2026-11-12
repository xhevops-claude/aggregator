using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Collector.Providers.SportM.Parsing;

internal static partial class CatalogPageParser
{
    private const string ProductLinkMarker = "ProductDetails?ProductID=";
    private const string CartCall = "OnAddToShoppingCartFromProdList(";

    public static IReadOnlyList<CatalogTile> Parse(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        var tiles = new List<CatalogTile>();
        foreach (var element in document.QuerySelectorAll("div.product_partial"))
        {
            var tile = ParseTile(element);
            if (tile is not null) tiles.Add(tile);
        }

        return tiles;
    }

    public static IReadOnlyList<string> ProductIds(string html)
    {
        return Parse(html).Select(tile => tile.ProductId).ToList();
    }

    private static CatalogTile? ParseTile(IElement element)
    {
        var link = element.QuerySelectorAll("a[href]").FirstOrDefault(IsProductLink);
        if (link is null) return null;

        var linkQuery = SportMQuery.Of(link.GetAttribute("href") ?? "");
        var productId = SportMQuery.Get(linkQuery, "ProductID")?.Trim() ?? "";
        if (productId.Length == 0) return null;

        var (price, oldPrice) = ReadPrices(element);
        return new CatalogTile
        {
            ProductId = productId,
            Name = ReadName(element, productId),
            Brand = NullIfEmpty(element.QuerySelector("img.ow-brand-image")?.GetAttribute("alt")),
            Group = SportMQuery.Get(linkQuery, "Grupa"),
            ImagePath = ReadImage(element),
            Price = price,
            OldPrice = oldPrice,
        };
    }

    private static bool IsProductLink(IElement link)
    {
        var href = link.GetAttribute("href") ?? "";
        return href.Contains(ProductLinkMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadName(IElement element, string productId)
    {
        var description = element.QuerySelectorAll("a").FirstOrDefault(a => a.Id == "Description " + productId);
        var name = SportMText.Clean(description?.TextContent);
        if (name.Length > 0) return name;

        return SportMText.Clean(element.QuerySelector("a.description-product")?.TextContent);
    }

    private static string? ReadImage(IElement element)
    {
        var sources = element.QuerySelectorAll("img").Select(img => img.GetAttribute("src") ?? "");
        return sources.FirstOrDefault(src => src.StartsWith("/ProductImages/", StringComparison.OrdinalIgnoreCase));
    }

    private static (decimal? Price, decimal? OldPrice) ReadPrices(IElement element)
    {
        var fromCall = ReadPricesFromCall(element);
        if (fromCall.Price is not null) return fromCall;

        var sale = SportMPrice.Parse(element.QuerySelector(".colorRed")?.TextContent);
        if (sale is not null)
        {
            var regular = element.QuerySelector(".textDecorLine, .pricePositionDecoration")?.TextContent;
            return (sale, SportMPrice.Parse(regular));
        }

        var box = element.QuerySelector(".price-box")?.TextContent ?? element.TextContent;
        var amount = Amount().Match(box);
        return (amount.Success ? SportMPrice.Parse(amount.Value) : null, null);
    }

    private static (decimal? Price, decimal? OldPrice) ReadPricesFromCall(IElement element)
    {
        var handlers = element.QuerySelectorAll("[onclick]").Select(e => e.GetAttribute("onclick") ?? "");
        var call = handlers.FirstOrDefault(handler => handler.Contains(CartCall, StringComparison.Ordinal));
        if (call is null) return (null, null);

        var arguments = QuotedArgument().Matches(call).Select(match => match.Groups[1].Value).ToList();
        if (arguments.Count < 5) return (null, null);

        var price = SportMPrice.Parse(arguments[2]);
        var discounted = arguments[4].Trim() == "1";
        var oldPrice = discounted ? SportMPrice.Parse(arguments[3]) : null;
        return (price, oldPrice);
    }

    private static string? NullIfEmpty(string? value)
    {
        var cleaned = SportMText.Clean(value);
        return cleaned.Length == 0 ? null : cleaned;
    }

    [GeneratedRegex(@"'([^']*)'")]
    private static partial Regex QuotedArgument();

    [GeneratedRegex(@"\d{1,3}(?:\.\d{3})*,\d{2}\s*ден")]
    private static partial Regex Amount();
}
