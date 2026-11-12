using Collector.Adapting;
using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Lake;
using Collector.Providers.SportM.Parsing;

namespace Collector.Providers.SportM.Mapping;

internal static class ProductPageMapper
{
    public static IEnumerable<Entity> Map(RawItem raw, AdaptContext context)
    {
        if (SportMUrls.HasContext(raw.Item.Url)) yield break;

        var page = ProductPageParser.Parse(raw.Text);
        if (page is null || !IsFootwear(page)) yield break;
        if (!SameProduct(raw, page)) yield break;

        var category = MapCategory(page, raw, context);
        if (category is not null) yield return category;

        var product = MapProduct(page, category?.Id, raw, context);
        yield return product;
        yield return MapListing(page, product.Id, raw, context);
    }

    private static bool IsFootwear(ProductPage page)
    {
        var classified = false;
        foreach (var crumb in page.Crumbs)
        {
            var query = SportMQuery.Of(crumb.Href);
            var group = SportMQuery.Get(query, "Grupa");
            if (group == SportMShoeListings.FootwearGroup) return true;
            if (group is not null || SportMQuery.Get(query, "Segment") is not null) classified = true;
        }

        return !classified;
    }

    private static bool SameProduct(RawItem raw, ProductPage page)
    {
        if (raw.Item.ProviderId.Length == 0) return true;
        return string.Equals(raw.Item.ProviderId, page.ProductId, StringComparison.OrdinalIgnoreCase);
    }

    private static Category? MapCategory(ProductPage page, RawItem raw, AdaptContext context)
    {
        if (page.Crumbs.Count == 0) return null;

        var path = string.Join('/', page.Crumbs.Select(crumb => crumb.Text));
        return new Category
        {
            Id = DeterministicId.For(context.ProjectSeed, context.Provider, "category", path),
            ProjectId = context.ProjectId,
            Namespace = context.Namespace,
            Provider = context.Provider,
            ProviderId = path,
            SourceUrl = raw.Item.Url,
            HarvestedAt = raw.Item.LastCheckedAt(),
            Name = page.Crumbs[^1].Text,
            Path = path,
        };
    }

    private static Product MapProduct(ProductPage page, Guid? categoryId, RawItem raw, AdaptContext context)
    {
        var images = page.ImagePaths.Select(path => SportMUrls.Absolute(context.Source, path)).ToList();
        return new Product
        {
            Id = ProductIdentity.Candidate(context.ProjectSeed, context.Provider, page.ProductId),
            ProjectId = context.ProjectId,
            Namespace = context.Namespace,
            Provider = context.Provider,
            ProviderId = page.ProductId,
            SourceUrl = raw.Item.Url,
            HarvestedAt = raw.Item.LastCheckedAt(),
            Name = page.Name,
            Brand = SportMBrands.Canonical(SportMBrands.FromName(page.Name)),
            Sku = page.ProductId,
            CategoryId = categoryId,
            Images = images,
        };
    }

    private static Listing MapListing(ProductPage page, Guid productId, RawItem raw, AdaptContext context)
    {
        var sizes = page.Sizes.Select(label => new Size { Label = label, InStock = true }).ToList();
        var availability = sizes.Count > 0 ? Availability.InStock : Availability.Unknown;
        var currency = context.Source.Currency.Length == 0 ? null : context.Source.Currency;

        return new Listing
        {
            Id = DeterministicId.For(context.ProjectSeed, context.Provider, "listing", page.ProductId),
            ProjectId = context.ProjectId,
            Namespace = context.Namespace,
            Provider = context.Provider,
            ProviderId = page.ProductId,
            SourceUrl = raw.Item.Url,
            HarvestedAt = raw.Item.LastCheckedAt(),
            ProductId = productId,
            Title = page.Name,
            Price = page.Price,
            OldPrice = page.OldPrice,
            Currency = currency,
            Availability = availability,
            Sizes = sizes,
            Url = SportMUrls.ProductPage(context.Source, page.ProductId),
            InfoCheckedAt = raw.Item.LastCheckedAt(),
            StockCheckedAt = raw.Item.LastCheckedAt(),
        };
    }
}
