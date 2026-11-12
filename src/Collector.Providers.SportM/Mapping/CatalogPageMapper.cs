using Collector.Adapting;
using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Lake;
using Collector.Providers.SportM.Parsing;

namespace Collector.Providers.SportM.Mapping;

internal static class CatalogPageMapper
{
    public static IEnumerable<Entity> Map(RawItem raw, AdaptContext context)
    {
        var pageQuery = SportMQuery.Of(raw.Item.Url);
        var filterBrand = SportMBrands.NameForCode(SportMQuery.Get(pageQuery, "Brend"));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tile in CatalogPageParser.Parse(raw.Text))
        {
            if (tile.Group != SportMShoeListings.FootwearGroup) continue;
            if (tile.Name.Length == 0) continue;
            if (!seen.Add(tile.ProductId)) continue;

            var product = MapProduct(tile, filterBrand, raw, context);
            yield return product;
            yield return MapListing(tile, product.Id, raw, context);
        }
    }

    private static Product MapProduct(CatalogTile tile, string? filterBrand, RawItem raw, AdaptContext context)
    {
        var brand = tile.Brand ?? filterBrand ?? SportMBrands.FromName(tile.Name);
        var images = new List<string>();
        if (tile.ImagePath is not null) images.Add(SportMUrls.Absolute(context.Source, tile.ImagePath));

        return new Product
        {
            Id = ProductIdentity.Candidate(context.ProjectSeed, context.Provider, tile.ProductId),
            ProjectId = context.ProjectId,
            Namespace = context.Namespace,
            Provider = context.Provider,
            ProviderId = tile.ProductId,
            SourceUrl = raw.Item.Url,
            HarvestedAt = raw.Item.LastCheckedAt(),
            Name = tile.Name,
            Brand = SportMBrands.Canonical(brand),
            Sku = tile.ProductId,
            Images = images,
        };
    }

    private static Listing MapListing(CatalogTile tile, Guid productId, RawItem raw, AdaptContext context)
    {
        var currency = context.Source.Currency.Length == 0 ? null : context.Source.Currency;
        return new Listing
        {
            Id = DeterministicId.For(context.ProjectSeed, context.Provider, "listing", tile.ProductId),
            ProjectId = context.ProjectId,
            Namespace = context.Namespace,
            Provider = context.Provider,
            ProviderId = tile.ProductId,
            SourceUrl = raw.Item.Url,
            HarvestedAt = raw.Item.LastCheckedAt(),
            ProductId = productId,
            Title = tile.Name,
            Price = tile.Price,
            OldPrice = tile.OldPrice,
            Currency = currency,
            Url = SportMUrls.ProductPage(context.Source, tile.ProductId),
            InfoCheckedAt = null,
            StockCheckedAt = null,
        };
    }
}
