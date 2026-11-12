using AngleSharp.Dom;
using Collector.Core.Entities;

namespace Collector.Providers.Nbshop;

public static class NbshopProductHtml
{
    public const string EuSizeSystem = "EU";

    public static decimal? OldPrice(IDocument document)
    {
        var block = document.QuerySelector("div.product-oldprice");
        if (block is null || block.ClassList.Contains("hide")) return null;

        var value = block.QuerySelector(".product-oldprice-value");
        return NbshopPrice.Parse(value?.TextContent);
    }

    public static string? ModelCode(IDocument document)
    {
        var code = document.QuerySelector(".product-model span")?.TextContent.Trim();
        return string.IsNullOrEmpty(code) ? null : code;
    }

    public static string? CategoryName(IDocument document)
    {
        var name = document.QuerySelector("div.category a[title]")?.GetAttribute("title")?.Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    public static List<Size> Sizes(IDocument document)
    {
        var sizes = new List<Size>();
        var list = document.QuerySelector("ul.product-attributes");
        if (list is null) return sizes;

        foreach (var item in list.Children)
        {
            if (item.LocalName != "li") continue;

            var size = Size(item);
            if (size is not null) sizes.Add(size);
        }

        return sizes;
    }

    private static Size? Size(IElement item)
    {
        var eu = Text(item.QuerySelector(".eur-size"));
        var original = Text(item.QuerySelector(".original-size"));
        var label = eu ?? original;
        if (label is null) return null;

        var ean = item.GetAttribute("data-combination-code")?.Trim();
        var isEu = eu is not null && char.IsAsciiDigit(eu[0]);
        return new Size
        {
            Label = label,
            System = isEu ? EuSizeSystem : null,
            Ean = string.IsNullOrEmpty(ean) ? null : ean,
            InStock = !item.ClassList.Contains("disabled"),
        };
    }

    private static string? Text(IElement? element)
    {
        var text = element?.TextContent.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
