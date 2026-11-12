using Collector.Core.Domains;
using Collector.Core.Entities;
using Collector.Data;

namespace Collector.Storing.Tests;

[Trait("Category", "Integration")]
public class StoreWriterMergeTests(RigDatabase rig) : IClassFixture<RigDatabase>
{
    [Fact]
    public async Task Restaging_with_missing_values_never_wipes_stored_fields()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var full = store.NewListing("sportm", "JQ5211") with
        {
            Title = "adidas VS PACE 2.0",
            Description = "Leather upper",
            Price = 4990.00m,
            OldPrice = 5990.00m,
            Currency = "MKD",
            Availability = Availability.InStock,
            InfoCheckedAt = FixedTime.Start,
        };
        await store.StageAndApplyAsync(full);

        var sparse = store.NewListing("sportm", "JQ5211") with { Title = "", Url = "", ProductId = null };
        await store.StageAndApplyAsync(sparse with { RefreshInterval = TimeSpan.FromHours(6) });

        var stored = await store.ListingAsync(full.Id);
        Assert.Equal("adidas VS PACE 2.0", stored.Title);
        Assert.Equal("Leather upper", stored.Description);
        Assert.Equal(4990.00m, stored.Price);
        Assert.Equal(5990.00m, stored.OldPrice);
        Assert.Equal("MKD", stored.Currency);
        Assert.Equal(Availability.InStock, stored.Availability);
        Assert.Equal(FixedTime.Start, stored.InfoCheckedAt);
        Assert.Equal(full.Url, stored.Url);
        Assert.Equal(full.ProductId, stored.ProductId);
        Assert.Equal(TimeSpan.FromHours(6), stored.RefreshInterval);
    }

    [Fact]
    public async Task Old_price_goes_with_the_price_it_was_seen_next_to()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var onSale = store.NewListing("sportm", "JQ5211") with { Price = 100m, OldPrice = 150m, Currency = "MKD" };
        await store.StageAndApplyAsync(onSale with { InfoCheckedAt = FixedTime.Start });

        await store.StageAndApplyAsync(onSale with { Price = null, OldPrice = null });
        var noPriceSeen = await store.ListingAsync(onSale.Id);
        Assert.Equal(100m, noPriceSeen.Price);
        Assert.Equal(150m, noPriceSeen.OldPrice);

        var saleOver = onSale with { Price = 150m, OldPrice = null, InfoCheckedAt = FixedTime.Start.AddDays(1) };
        await store.StageAndApplyAsync(saleOver);
        var stored = await store.ListingAsync(onSale.Id);
        Assert.Equal(150m, stored.Price);
        Assert.Null(stored.OldPrice);
        Assert.Equal("MKD", stored.Currency);
        Assert.False((await store.RawAsync(CollectionNames.Listings, onSale.Id)).Contains("oldPrice"));

        await store.StageAndApplyAsync(onSale with { Price = 90m, OldPrice = 150m });
        Assert.Equal(150m, (await store.ListingAsync(onSale.Id)).OldPrice);
    }

    [Fact]
    public async Task Product_lists_and_domain_fields_merge_without_losing_values()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var first = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005") with
        {
            Images = ["https://sportm.example/a.jpg"],
            Fields = new ShoeFields { ModelCode = "LSH59", Colourway = "white" },
        };
        var applied = await store.StageAndApplyAsync(first);

        var second = first with { Images = [], Fields = new ShoeFields { Colourway = "black" } };
        await store.StageAndApplyAsync(second);

        var product = await store.ProductAsync(applied.EntityId!.Value);
        Assert.Equal(["https://sportm.example/a.jpg"], product.Images);
        var fields = Assert.IsType<ShoeFields>(product.Fields);
        Assert.Equal("LSH59", fields.ModelCode);
        Assert.Equal("black", fields.Colourway);
    }

    [Fact]
    public async Task Identity_and_created_at_come_from_the_first_apply_updated_at_from_the_last()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var sportm = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005");
        var applied = await store.StageAndApplyAsync(sportm);

        store.Time.Advance(TimeSpan.FromHours(2));
        await store.StageAndApplyAsync(store.NewProduct("sportvision", "8731122", "adidas", "HP6005"));

        var product = await store.ProductAsync(applied.EntityId!.Value);
        Assert.Equal("sportm", product.Provider);
        Assert.Equal("JQ5211", product.ProviderId);
        Assert.Equal("clothing/shoes/sportm", product.Namespace);
        Assert.Equal(FixedTime.Start, product.CreatedAt);
        Assert.Equal(FixedTime.Start.AddHours(2), product.UpdatedAt);
    }

    [Fact]
    public async Task Sizes_change_only_with_a_stock_check_or_when_sizes_are_staged()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var listing = store.NewListing("sportm", "JQ5211");
        var sizes = new List<Size> { new() { Label = "42", InStock = true }, new() { Label = "43", InStock = false } };

        await store.StageAndApplyAsync(listing with { Sizes = sizes, StockCheckedAt = FixedTime.Start });
        await store.StageAndApplyAsync(listing with { Sizes = [] });
        Assert.Equal(2, (await store.ListingAsync(listing.Id)).Sizes.Count);

        await store.StageAndApplyAsync(listing with { Sizes = [new Size { Label = "44", InStock = true }] });
        Assert.Equal("44", Assert.Single((await store.ListingAsync(listing.Id)).Sizes).Label);

        await store.StageAndApplyAsync(listing with { Sizes = [], StockCheckedAt = FixedTime.Start.AddDays(1) });
        var stored = await store.ListingAsync(listing.Id);
        Assert.Empty(stored.Sizes);
        Assert.Equal(FixedTime.Start.AddDays(1), stored.StockCheckedAt);

        var raw = await store.RawAsync(CollectionNames.Listings, listing.Id);
        Assert.True(raw.Contains("sizes"));
    }

    [Fact]
    public async Task Any_listing_in_stock_follows_availability_and_sizes()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var applied = await store.StageAndApplyAsync(store.NewProduct("sportm", "JQ5211", "adidas", "HP6005"));
        var productId = applied.EntityId!.Value;
        var sportm = store.NewListing("sportm", "JQ5211");
        var sportvision = store.NewListing("sportvision", "8731122") with { ProductId = productId };

        await store.StageAndApplyAsync(sportm with { Availability = Availability.OutOfStock });
        Assert.False((await store.ProductAsync(productId)).AnyListingInStock);

        var sizeInStock = new Size { Label = "42", InStock = true };
        await store.StageAndApplyAsync(sportvision with { Sizes = [sizeInStock], StockCheckedAt = FixedTime.Start });
        Assert.True((await store.ProductAsync(productId)).AnyListingInStock);

        var sizeGone = sizeInStock with { InStock = false };
        await store.StageAndApplyAsync(sportvision with { Sizes = [sizeGone], StockCheckedAt = FixedTime.Start });
        Assert.False((await store.ProductAsync(productId)).AnyListingInStock);

        await store.StageAndApplyAsync(sportm with { Availability = Availability.InStock });
        Assert.True((await store.ProductAsync(productId)).AnyListingInStock);
    }

    [Fact]
    public async Task Product_applied_after_its_in_stock_listing_is_marked_in_stock()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var listing = store.NewListing("sportm", "JQ5211") with { Availability = Availability.InStock };

        await store.StageAndApplyAsync(listing);
        var applied = await store.StageAndApplyAsync(store.NewProduct("sportm", "JQ5211", "adidas", "HP6005"));

        Assert.True((await store.ProductAsync(applied.EntityId!.Value)).AnyListingInStock);
    }

    [Fact]
    public async Task Category_is_upserted_by_id()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var category = store.NewCategory("sportm", "obuvki/patiki");

        await store.StageAndApplyAsync(category);
        await store.StageAndApplyAsync(category with { Name = "Патики", Path = "" });

        var stored = await store.RawAsync(CollectionNames.Categories, category.Id);
        Assert.Equal("Патики", stored["name"].AsString);
        Assert.Equal("obuvki/patiki", stored["path"].AsString);
    }
}
