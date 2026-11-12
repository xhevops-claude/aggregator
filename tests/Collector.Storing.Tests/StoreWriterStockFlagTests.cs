using Collector.Core.Entities;
using Collector.Data;

namespace Collector.Storing.Tests;

[Trait("Category", "Integration")]
public class StoreWriterStockFlagTests(RigDatabase rig) : IClassFixture<RigDatabase>
{
    [Fact]
    public async Task A_product_seen_only_on_catalog_tiles_has_unknown_stock()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var applied = await store.StageAndApplyAsync(store.NewProduct("intersport", "4061612", "adidas", "JS4327"));
        var productId = applied.EntityId!.Value;

        await store.StageAndApplyAsync(store.NewListing("intersport", "4061612") with { Price = 3990m });

        Assert.Null((await store.ProductAsync(productId)).AnyListingInStock);
        Assert.False((await store.RawAsync(CollectionNames.Products, productId)).Contains("anyListingInStock"));
    }

    [Fact]
    public async Task A_stock_check_without_a_verdict_keeps_stock_unknown()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var applied = await store.StageAndApplyAsync(store.NewProduct("sportm", "LA1235", "adidas", "LA1235"));
        var noSizeBlock = store.NewListing("sportm", "LA1235") with { StockCheckedAt = FixedTime.Start };

        await store.StageAndApplyAsync(noSizeBlock);

        Assert.Null((await store.ProductAsync(applied.EntityId!.Value)).AnyListingInStock);
    }

    [Fact]
    public async Task One_checked_listing_out_of_stock_makes_the_product_out_of_stock_until_one_is_in_stock()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var applied = await store.StageAndApplyAsync(store.NewProduct("sportm", "JQ5211", "adidas", "HP6005"));
        var productId = applied.EntityId!.Value;
        var tile = store.NewListing("sportvision", "8731122") with { ProductId = productId };
        var sportm = store.NewListing("sportm", "JQ5211");

        await store.StageAndApplyAsync(tile);
        Assert.Null((await store.ProductAsync(productId)).AnyListingInStock);

        await store.StageAndApplyAsync(sportm with { Availability = Availability.OutOfStock });
        Assert.False((await store.ProductAsync(productId)).AnyListingInStock);

        await store.StageAndApplyAsync(tile with { Sizes = [new Size { Label = "42", InStock = true }] });
        Assert.True((await store.ProductAsync(productId)).AnyListingInStock);
    }
}
