using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Collector.Core.Entities;

namespace Collector.Providers.Nbshop;

public sealed record NbshopProductPage
{
    public string ProviderId { get; init; } = "";
    public string Category { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Brand { get; init; }
    public string? Sku { get; init; }
    public string? Description { get; init; }
    public List<string> Images { get; init; } = [];
    public decimal? Price { get; init; }
    public decimal? OldPrice { get; init; }
    public string? Currency { get; init; }
    public Availability Availability { get; init; }
    public List<Size> Sizes { get; init; } = [];
    public string? ModelCode { get; init; }

    public static NbshopProductPage Parse(string html, NbshopProductUrl url, string? fallbackCurrency)
    {
        var document = new HtmlParser().ParseDocument(html);
        var blocks = NbshopJsonLd.Blocks(document);
        var found = NbshopJsonLd.FirstOfType(blocks, "Product");
        if (found is not { } product) throw new InvalidDataException($"No Product JSON-LD on {url.Url}");

        var providerId = NbshopProductJson.Text(product, "productID") ?? url.ProviderId;
        var offer = NbshopProductJson.Offer(product);
        var price = PriceOf(offer);
        var currency = CurrencyOf(offer) ?? NullIfEmpty(fallbackCurrency);
        var availability = AvailabilityOf(offer);

        return new NbshopProductPage
        {
            ProviderId = providerId,
            Category = url.Category,
            CategoryName = CategoryNameOf(document, blocks, url.Category),
            Name = NbshopProductJson.Text(product, "name") ?? "",
            Brand = NbshopProductJson.Brand(product),
            Sku = NbshopProductJson.Text(product, "sku"),
            Description = NbshopProductJson.Text(product, "description"),
            Images = NbshopProductJson.Images(product),
            Price = price,
            OldPrice = NbshopProductHtml.OldPrice(document),
            Currency = currency,
            Availability = availability,
            Sizes = NbshopProductHtml.Sizes(document),
            ModelCode = NbshopProductHtml.ModelCode(document),
        };
    }

    private static decimal? PriceOf(JsonElement? offer)
    {
        if (offer is not { } value) return null;
        if (!value.TryGetProperty("price", out var price)) return null;

        return NbshopPrice.Parse(price);
    }

    private static string? CurrencyOf(JsonElement? offer)
    {
        if (offer is not { } value) return null;

        return NbshopProductJson.Text(value, "priceCurrency");
    }

    private static Availability AvailabilityOf(JsonElement? offer)
    {
        if (offer is not { } value) return Availability.Unknown;

        return NbshopProductJson.ParseAvailability(NbshopProductJson.Text(value, "availability"));
    }

    private static string CategoryNameOf(IDocument document, IReadOnlyList<JsonElement> blocks, string category)
    {
        var fromBreadcrumbs = BreadcrumbName(blocks, category);
        if (fromBreadcrumbs is not null) return fromBreadcrumbs;

        return NbshopProductHtml.CategoryName(document) ?? category;
    }

    private static string? BreadcrumbName(IReadOnlyList<JsonElement> blocks, string category)
    {
        var found = NbshopJsonLd.FirstOfType(blocks, "BreadcrumbList");
        if (found is not { } breadcrumbs) return null;
        if (!breadcrumbs.TryGetProperty("itemListElement", out var items)) return null;
        if (items.ValueKind != JsonValueKind.Array) return null;

        foreach (var item in items.EnumerateArray())
        {
            var url = NbshopProductJson.Text(item, "item");
            if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) continue;

            var lastSegment = uri.AbsolutePath.TrimEnd('/').Split('/')[^1];
            if (lastSegment == category) return NbshopProductJson.Text(item, "name");
        }

        return null;
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
