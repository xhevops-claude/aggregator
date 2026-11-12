using Collector.Core.Domains;
using Collector.Core.Entities;
using Collector.Core.Ids;

namespace Collector.Storing.Tests;

[Trait("Category", "Integration")]
public class StoreWriterMatchingTests(RigDatabase rig) : IClassFixture<RigDatabase>
{
    [Fact]
    public async Task Same_brand_and_sku_from_two_providers_make_one_product_with_two_listings()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var sportm = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005");
        var sportvision = store.NewProduct("sportvision", "8731122", "Adidas", "hp6005");

        await store.StageAndApplyAsync(sportm);
        await store.StageAndApplyAsync(store.NewListing("sportm", "JQ5211"));
        await store.StageAndApplyAsync(sportvision);
        await store.StageAndApplyAsync(store.NewListing("sportvision", "8731122"));

        var canonical = ProductIdentity.Canonical(store.Project.Seed, "adidas", "HP6005");
        var product = Assert.Single(await store.ProductsAsync());
        Assert.Equal(canonical, product.Id);
        Assert.Equal("adidas", product.Brand);
        Assert.Equal("sportm", product.Provider);
        Assert.Equal(new[] { sportm.Id, sportvision.Id }.Order(), product.Aliases.Order());
        Assert.Equal(canonical, (await store.ListingAsync(store.NewListing("sportm", "JQ5211").Id)).ProductId);
        Assert.Equal(canonical, (await store.ListingAsync(store.NewListing("sportvision", "8731122").Id)).ProductId);
    }

    [Fact]
    public async Task Listing_staged_before_its_product_ends_up_on_the_canonical_product()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var listing = store.NewListing("sportm", "JQ5211");
        var product = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005");

        await store.StageAndApplyAsync(listing);
        Assert.Equal(product.Id, (await store.ListingAsync(listing.Id)).ProductId);

        var applied = await store.StageAndApplyAsync(product);

        var canonical = ProductIdentity.Canonical(store.Project.Seed, "adidas", "HP6005");
        Assert.Equal(canonical, applied.EntityId);
        Assert.Equal(canonical, (await store.ListingAsync(listing.Id)).ProductId);

        await store.StageAndApplyAsync(listing with { Title = "restaged" });
        Assert.Equal(canonical, (await store.ListingAsync(listing.Id)).ProductId);
    }

    [Fact]
    public async Task Product_without_sku_folds_into_the_canonical_product_once_its_sku_is_known()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var bare = store.NewProduct("sportm", "JQ5211", null, null) with
        {
            Name = "VS PACE 2.0",
            Images = ["https://sportm.example/img/JQ5211.jpg"],
            Fields = new ShoeFields { Colourway = "white" },
        };
        await store.StageAndApplyAsync(bare);
        await store.StageAndApplyAsync(store.NewListing("sportm", "JQ5211"));

        var detailed = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005") with { Name = "adidas VS PACE 2.0" };
        await store.StageAndApplyAsync(detailed);
        await store.StageAndApplyAsync(bare with { Name = "VS PACE 2.0 again" });

        var canonical = ProductIdentity.Canonical(store.Project.Seed, "adidas", "HP6005");
        var product = Assert.Single(await store.ProductsAsync());
        Assert.Equal(canonical, product.Id);
        Assert.Equal([bare.Id], product.Aliases);
        Assert.Equal("adidas", product.Brand);
        Assert.Equal("HP6005", product.Sku);
        Assert.Equal("VS PACE 2.0 again", product.Name);
        Assert.Equal(["https://sportm.example/img/JQ5211.jpg"], product.Images);
        Assert.Equal("white", Assert.IsType<ShoeFields>(product.Fields).Colourway);
        Assert.Equal(canonical, (await store.ListingAsync(store.NewListing("sportm", "JQ5211").Id)).ProductId);
    }

    [Fact]
    public async Task Brand_without_sku_stays_on_the_candidate_and_later_fills_an_empty_brand()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var bare = store.NewProduct("sportm", "JQ5211", null, null) with { Name = "VS PACE 2.0" };

        await store.StageAndApplyAsync(bare);
        await store.StageAndApplyAsync(bare with { Brand = "Adidas" });
        await store.StageAndApplyAsync(bare with { Brand = "ADIDAS" });

        var product = Assert.Single(await store.ProductsAsync());
        Assert.Equal(bare.Id, product.Id);
        Assert.Equal("Adidas", product.Brand);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Corrected_sku_moves_the_listing_and_drops_the_product_left_empty(bool wrongFirst)
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var wrong = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005");
        var right = wrong with { Sku = "HP6006", HarvestedAt = FixedTime.Start.AddSeconds(10) };
        var listing = store.NewListing("sportm", "JQ5211") with { Availability = Availability.InStock };
        await store.StageAndApplyAsync(wrongFirst ? wrong : right);
        await store.StageAndApplyAsync(listing);

        await store.StageAndApplyAsync(wrongFirst ? right : wrong);

        var corrected = ProductIdentity.Canonical(store.Project.Seed, "adidas", "HP6006");
        var product = Assert.Single(await store.ProductsAsync());
        Assert.Equal(corrected, product.Id);
        Assert.Equal("HP6006", product.Sku);
        Assert.Equal([wrong.Id], product.Aliases);
        Assert.True(product.AnyListingInStock);
        Assert.Equal(corrected, (await store.ListingAsync(listing.Id)).ProductId);
    }

    [Fact]
    public async Task Corrected_sku_leaves_the_other_providers_on_the_old_product()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var wrong = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005");
        var sportvision = store.NewProduct("sportvision", "8731122", "adidas", "HP6005");
        var sportmListing = store.NewListing("sportm", "JQ5211") with { Availability = Availability.InStock };
        var otherListing = store.NewListing("sportvision", "8731122") with { Availability = Availability.OutOfStock };
        await store.StageAndApplyAsync(wrong);
        await store.StageAndApplyAsync(sportmListing);
        await store.StageAndApplyAsync(sportvision);
        await store.StageAndApplyAsync(otherListing);

        await store.StageAndApplyAsync(wrong with { Sku = "HP6006" });

        var old = ProductIdentity.Canonical(store.Project.Seed, "adidas", "HP6005")!.Value;
        var corrected = ProductIdentity.Canonical(store.Project.Seed, "adidas", "HP6006")!.Value;
        Assert.Equal(2, (await store.ProductsAsync()).Count);
        var oldProduct = await store.ProductAsync(old);
        Assert.Equal([sportvision.Id], oldProduct.Aliases);
        Assert.False(oldProduct.AnyListingInStock);
        Assert.Equal(old, (await store.ListingAsync(otherListing.Id)).ProductId);
        var correctedProduct = await store.ProductAsync(corrected);
        Assert.Equal([wrong.Id], correctedProduct.Aliases);
        Assert.True(correctedProduct.AnyListingInStock);
        Assert.Equal(corrected, (await store.ListingAsync(sportmListing.Id)).ProductId);
    }

    [Fact]
    public async Task Projects_never_share_products()
    {
        var first = await StoreScenario.CreateAsync(rig.Database);
        var second = await StoreScenario.CreateAsync(rig.Database);

        await first.StageAndApplyAsync(first.NewProduct("sportm", "JQ5211", "adidas", "HP6005"));
        await second.StageAndApplyAsync(second.NewProduct("sportm", "JQ5211", "adidas", "HP6005"));

        var inFirst = Assert.Single(await first.ProductsAsync());
        var inSecond = Assert.Single(await second.ProductsAsync());
        Assert.NotEqual(inFirst.Id, inSecond.Id);
    }
}
