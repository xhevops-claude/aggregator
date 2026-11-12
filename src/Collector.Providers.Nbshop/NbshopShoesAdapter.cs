using Collector.Adapting;
using Collector.Core.Domains;
using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Lake;

namespace Collector.Providers.Nbshop;

public sealed class NbshopShoesAdapter : IAdapter
{
    public string Name => NbshopNames.ShoesAdapter;
    public string Platform => NbshopNames.Platform;
    public string Domain => DomainNames.Shoes;

    public IReadOnlyCollection<string> RawKinds { get; } =
    [
        NbshopNames.SitemapKind,
        NbshopNames.ListingPageKind,
        NbshopNames.ProductPageKind,
    ];

    public IEnumerable<Entity> Adapt(RawItem raw, AdaptContext context)
    {
        return raw.Item.Kind switch
        {
            NbshopNames.SitemapKind => FromSitemap(raw, context),
            NbshopNames.ListingPageKind => FromListingPage(raw, context),
            NbshopNames.ProductPageKind => FromProductPage(raw, context),
            _ => [],
        };
    }

    private static List<Entity> FromSitemap(RawItem raw, AdaptContext context)
    {
        var entities = new List<Entity>();
        foreach (var url in NbshopSitemap.ProductUrls(raw.Text))
        {
            if (!NbshopShoeCategories.Contains(url.Category)) continue;

            entities.Add(DiscoveredListing(raw, context, url.ProviderId, url.Url, url.TitleFromSlug));
        }

        return entities;
    }

    private static List<Entity> FromListingPage(RawItem raw, AdaptContext context)
    {
        var entities = new List<Entity>();
        var pageUrl = PageUrl(raw, context);
        foreach (var tile in NbshopListingPage.Parse(raw.Text, pageUrl).Tiles)
        {
            if (!NbshopShoeCategories.Contains(tile.Category)) continue;

            entities.Add(DiscoveredListing(raw, context, tile.ProviderId, tile.Url, tile.Title));
        }

        return entities;
    }

    private static List<Entity> FromProductPage(RawItem raw, AdaptContext context)
    {
        if (!NbshopProductUrl.TryParse(raw.Item.Url, out var url))
        {
            throw new InvalidDataException($"{raw.Item.Url} is not an NBSHOP product URL");
        }

        if (!NbshopShoeCategories.Contains(url.Category)) return [];

        var page = NbshopProductPage.Parse(raw.Text, url, context.Source.Currency);
        var productId = ProductIdentity.Candidate(context.ProjectSeed, context.Provider, page.ProviderId);
        var categoryId = DeterministicId.For(context.ProjectSeed, context.Provider, "category", page.Category);

        var product = new Product
        {
            Id = productId,
            Name = page.Name,
            Brand = page.Brand,
            Sku = page.Sku,
            CategoryId = categoryId,
            Images = page.Images,
            Fields = new ShoeFields { ModelCode = page.ModelCode },
        };

        var listing = new Listing
        {
            Id = ListingId(context, page.ProviderId),
            ProductId = productId,
            Title = page.Name,
            Description = page.Description,
            Price = page.Price,
            OldPrice = page.OldPrice,
            Currency = page.Currency,
            Availability = page.Availability,
            Sizes = page.Sizes,
            Url = url.Url,
            InfoCheckedAt = raw.Item.LastCheckedAt(),
            StockCheckedAt = raw.Item.LastCheckedAt(),
        };

        var category = new Category
        {
            Id = categoryId,
            Name = page.CategoryName,
            Path = page.Category,
        };

        return
        [
            Stamp(product, raw, context, page.ProviderId),
            Stamp(listing, raw, context, page.ProviderId),
            Stamp(category, raw, context, page.Category),
        ];
    }

    private static Entity DiscoveredListing(
        RawItem raw, AdaptContext context, string providerId, string url, string title)
    {
        var listing = new Listing
        {
            Id = ListingId(context, providerId),
            ProductId = ProductIdentity.Candidate(context.ProjectSeed, context.Provider, providerId),
            Title = title,
            Url = url,
        };
        return Stamp(listing, raw, context, providerId);
    }

    private static Entity Stamp(Entity entity, RawItem raw, AdaptContext context, string providerId)
    {
        return entity with
        {
            ProjectId = context.ProjectId,
            Namespace = context.Namespace,
            Provider = context.Provider,
            ProviderId = providerId,
            SourceUrl = raw.Item.Url,
            HarvestedAt = raw.Item.LastCheckedAt(),
        };
    }

    private static Guid ListingId(AdaptContext context, string providerId) =>
        DeterministicId.For(context.ProjectSeed, context.Provider, "listing", providerId);

    private static Uri PageUrl(RawItem raw, AdaptContext context)
    {
        if (NbshopUrl.TryHttp(raw.Item.Url, out var url)) return url;
        if (NbshopUrl.TryHttp(context.Source.BaseUrl, out var baseUrl)) return baseUrl;

        throw new InvalidDataException($"Listing page {raw.Item.Key} has no usable URL");
    }
}
