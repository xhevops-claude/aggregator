using Collector.Core.Entities;
using Collector.Core.Ids;

namespace Collector.Storing.Tests;

[Trait("Category", "Integration")]
public class StoreWriterConcurrencyTests(RigDatabase rig) : IClassFixture<RigDatabase>
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Parallel_applies_for_one_sku_end_in_one_product_that_every_listing_points_to(int round)
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var stagingIds = new List<Guid>();
        var listingIds = new List<Guid>();
        for (var shop = 0; shop < 12; shop++)
        {
            var provider = $"shop{shop}";
            var listing = store.NewListing(provider, $"P{round}") with { Availability = Availability.InStock };
            listingIds.Add(listing.Id);
            stagingIds.Add(await store.StageAsync(listing));
            stagingIds.Add(await store.StageAsync(store.NewProduct(provider, $"P{round}", "adidas", "HP6005")));
        }

        var results = await Task.WhenAll(stagingIds.Select(store.ApplyAsync));

        Assert.All(results, result => Assert.Equal(ApplyOutcome.Applied, result.Outcome));
        var canonical = ProductIdentity.Canonical(store.Project.Seed, "adidas", "HP6005");
        var product = Assert.Single(await store.ProductsAsync());
        Assert.Equal(canonical, product.Id);
        Assert.Equal(12, product.Aliases.Count);
        Assert.True(product.AnyListingInStock);
        foreach (var listingId in listingIds) Assert.Equal(canonical, (await store.ListingAsync(listingId)).ProductId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Parallel_stock_changes_leave_the_flag_matching_the_listings(int round)
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var applied = await store.StageAndApplyAsync(store.NewProduct("shop0", $"P{round}", "adidas", "HP6005"));
        var productId = applied.EntityId!.Value;
        var listings = new List<Listing>();
        for (var shop = 0; shop < 12; shop++)
        {
            var listing = store.NewListing($"shop{shop}", $"P{round}") with { ProductId = productId };
            listings.Add(listing);
            await store.StageAndApplyAsync(listing with { Availability = Availability.InStock });
        }

        var outOfStock = new List<Guid>();
        foreach (var listing in listings)
        {
            outOfStock.Add(await store.StageAsync(listing with { Availability = Availability.OutOfStock }));
        }

        await Task.WhenAll(outOfStock.Select(store.ApplyAsync));

        Assert.False((await store.ProductAsync(productId)).AnyListingInStock);
    }
}
