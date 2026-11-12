using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Core.Platform;
using Collector.Lake;
using Collector.Providers.SportM.Parsing;

namespace Collector.Providers.SportM.Tests;

public sealed class ShoesAdapterProductTests
{
    private const string SportMProduct = "https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=";
    private const string IntersportProduct = "https://intersport.mk/ProductCatalog/ProductDetails?ProductID=";
    private const string SportMImages = "https://sport-m.com.mk/ProductImages/";
    private const string IntersportImages = "https://intersport.mk/ProductImages/";

    private readonly SportMShoesAdapter adapter = new();

    [Fact]
    public void SportMShoePageBecomesCategoryProductAndListing()
    {
        var raw = ProductRaw(Shops.SportM, "IH7234", SportMProduct + "IH7234", SampleFiles.SportMProductIH7234);

        var entities = adapter.Adapt(raw, Shops.Context(Shops.SportM)).ToList();

        Assert.Equal(3, entities.Count);
        var category = Assert.IsType<Category>(entities[0]);
        var product = Assert.IsType<Product>(entities[1]);
        var listing = Assert.IsType<Listing>(entities[2]);

        var categoryId = DeterministicId.For(Shops.Seed, "sportm", "category", "Мажи/Обувки");
        Assert.Equal(categoryId, category.Id);
        Assert.Equal(Shops.ProjectId, category.ProjectId);
        Assert.Equal("clothing/shoes/sportm", category.Namespace);
        Assert.Equal("sportm", category.Provider);
        Assert.Equal("Мажи/Обувки", category.ProviderId);
        Assert.Equal(SportMProduct + "IH7234", category.SourceUrl);
        Assert.Equal(Shops.HarvestedAt, category.HarvestedAt);
        Assert.Equal("Обувки", category.Name);
        Assert.Equal("Мажи/Обувки", category.Path);
        Assert.Null(category.ParentId);

        var productId = ProductIdentity.Candidate(Shops.Seed, "sportm", "IH7234");
        Assert.Equal(productId, product.Id);
        Assert.Equal(Shops.ProjectId, product.ProjectId);
        Assert.Equal("clothing/shoes/sportm", product.Namespace);
        Assert.Equal("sportm", product.Provider);
        Assert.Equal("IH7234", product.ProviderId);
        Assert.Equal(SportMProduct + "IH7234", product.SourceUrl);
        Assert.Equal(Shops.HarvestedAt, product.HarvestedAt);
        Assert.Equal("Adidas PREDATOR PRO FT FG", product.Name);
        Assert.Equal("adidas", product.Brand);
        Assert.Equal("IH7234", product.Sku);
        Assert.Equal(categoryId, product.CategoryId);
        string[] images =
        [
            SportMImages + "IH7234.webp",
            SportMImages + "IH7234_1.webp",
            SportMImages + "IH7234_2.webp",
            SportMImages + "IH7234_3.webp",
            SportMImages + "IH7234_4.webp",
            SportMImages + "IH7234_5.webp",
            SportMImages + "IH7234_6.webp",
            SportMImages + "IH7234_7.webp",
        ];
        Assert.Equal(images, product.Images);

        Assert.Equal(DeterministicId.For(Shops.Seed, "sportm", "listing", "IH7234"), listing.Id);
        Assert.Equal(Shops.ProjectId, listing.ProjectId);
        Assert.Equal("clothing/shoes/sportm", listing.Namespace);
        Assert.Equal("sportm", listing.Provider);
        Assert.Equal("IH7234", listing.ProviderId);
        Assert.Equal(SportMProduct + "IH7234", listing.SourceUrl);
        Assert.Equal(Shops.HarvestedAt, listing.HarvestedAt);
        Assert.Equal(productId, listing.ProductId);
        Assert.Equal("Adidas PREDATOR PRO FT FG", listing.Title);
        Assert.Equal(9899m, listing.Price);
        Assert.Null(listing.OldPrice);
        Assert.Equal("MKD", listing.Currency);
        Assert.Equal(Availability.InStock, listing.Availability);
        Assert.Equal(["7.5", "8.5", "9", "9.5", "10.5"], listing.Sizes.Select(size => size.Label));
        Assert.All(listing.Sizes, size => Assert.True(size.InStock));
        Assert.All(listing.Sizes, size => Assert.Null(size.System));
        Assert.Equal(SportMProduct + "IH7234", listing.Url);
        Assert.Equal(Shops.HarvestedAt, listing.InfoCheckedAt);
        Assert.Equal(Shops.HarvestedAt, listing.StockCheckedAt);
    }

    [Fact]
    public void ReCheckOfAnUnchangedPageMovesBothListingDatesAndTheObservationTime()
    {
        var checkedAt = Shops.HarvestedAt.AddDays(3);
        var raw = ProductRaw(Shops.SportM, "IH7234", SportMProduct + "IH7234", SampleFiles.SportMProductIH7234);
        var rechecked = raw with { Item = raw.Item with { CheckedAt = checkedAt } };

        var entities = adapter.Adapt(rechecked, Shops.Context(Shops.SportM)).ToList();

        var listing = Assert.Single(entities.OfType<Listing>());
        Assert.Equal(checkedAt, listing.InfoCheckedAt);
        Assert.Equal(checkedAt, listing.StockCheckedAt);
        Assert.All(entities, entity => Assert.Equal(checkedAt, entity.HarvestedAt));
    }

    [Fact]
    public void CatalogTilesAreNotChecksEvenWhenTheirPageWasReChecked()
    {
        var url = "https://intersport.mk/ProductCatalog?Segment=28&Grupa=01&Page=1";
        var sample = SampleFiles.IntersportMenShoesPage1;
        var raw = Shops.Raw(Shops.Intersport, "catalog-page", "Segment=28&Grupa=01/page-1", url, sample);
        var rechecked = raw with { Item = raw.Item with { CheckedAt = Shops.HarvestedAt.AddDays(3) } };

        var listings = adapter.Adapt(rechecked, Shops.Context(Shops.Intersport)).OfType<Listing>().ToList();

        Assert.NotEmpty(listings);
        Assert.All(listings, listing => Assert.Null(listing.InfoCheckedAt));
        Assert.All(listings, listing => Assert.Null(listing.StockCheckedAt));
    }

    [Fact]
    public void SportMDiscountedPageKeepsTheOldPrice()
    {
        var url = SportMProduct + "IB2750-001";
        var raw = ProductRaw(Shops.SportM, "IB2750-001", url, SampleFiles.SportMProductIB2750);

        var entities = adapter.Adapt(raw, Shops.Context(Shops.SportM)).ToList();

        var product = Assert.Single(entities.OfType<Product>());
        var listing = Assert.Single(entities.OfType<Listing>());
        Assert.Equal("NIKE CHARGE SUEDE", product.Name);
        Assert.Equal("Nike", product.Brand);
        Assert.Equal(6, product.Images.Count);
        Assert.Equal(SportMImages + "IB2750-001_5.webp", product.Images[^1]);
        Assert.Equal(3599m, listing.Price);
        Assert.Equal(4499m, listing.OldPrice);
        string[] sizes = ["7.5", "8", "8.5", "9", "9.5", "10", "10.5", "11", "12"];
        Assert.Equal(sizes, listing.Sizes.Select(size => size.Label));
    }

    [Fact]
    public void IntersportGalleryNamesWithSpacesArePercentEncoded()
    {
        var url = IntersportProduct + "JQ6920";
        var raw = ProductRaw(Shops.Intersport, "JQ6920", url, SampleFiles.IntersportProductJQ6920);

        var entities = adapter.Adapt(raw, Shops.Context(Shops.Intersport)).ToList();

        var category = Assert.Single(entities.OfType<Category>());
        var product = Assert.Single(entities.OfType<Product>());
        var listing = Assert.Single(entities.OfType<Listing>());
        string[] images =
        [
            IntersportImages + "JQ6920.webp",
            IntersportImages + "JQ6920_3_FOOTWEAR_Photography_Top%20Portrait%20View_white.webp",
            IntersportImages + "JQ6920_5_FOOTWEAR_Photography_Side%20Medial%20Center%20View_white.webp",
            IntersportImages + "JQ6920_6_FOOTWEAR_Photography_Front%20Lateral%20Top%20View_white.webp",
            IntersportImages + "JQ6920_7_FOOTWEAR_Photography_Back%20Lateral%20Top%20View_white.webp",
            IntersportImages + "JQ6920_8_FOOTWEAR_Photography_Detail%20View%201_white.webp",
            IntersportImages + "JQ6920_9_FOOTWEAR_Photography_Detail%20View%202_white.webp",
        ];
        Assert.Equal(images, product.Images);
        Assert.Equal("Adidas ULTRARUN 5 TR", product.Name);
        Assert.Equal("adidas", product.Brand);
        Assert.Equal("МАЖИ/Обувки", category.Path);
        var categoryId = DeterministicId.For(Shops.Seed, "intersport", "category", "МАЖИ/Обувки");
        Assert.Equal(categoryId, product.CategoryId);
        Assert.Equal(4999m, listing.Price);
        Assert.Null(listing.OldPrice);
        Assert.Equal(["7.5", "8", "8.5", "9", "9.5", "10.5"], listing.Sizes.Select(size => size.Label));
        Assert.Equal(IntersportProduct + "JQ6920", listing.Url);
    }

    [Fact]
    public void IntersportDiscountedKidsShoe()
    {
        var url = IntersportProduct + "400603N-BKYL";
        var raw = ProductRaw(Shops.Intersport, "400603N-BKYL", url, SampleFiles.IntersportProductSkechers);

        var entities = adapter.Adapt(raw, Shops.Context(Shops.Intersport)).ToList();

        var category = Assert.Single(entities.OfType<Category>());
        var product = Assert.Single(entities.OfType<Product>());
        var listing = Assert.Single(entities.OfType<Listing>());
        Assert.Equal("ДЕЦА/Обувки", category.Path);
        Assert.Equal("Обувки", category.Name);
        Assert.Equal("Skechers VORTEX 2.0 - ZORENTO", product.Name);
        Assert.Equal("Skechers", product.Brand);
        Assert.Equal(4, product.Images.Count);
        Assert.Equal(2239m, listing.Price);
        Assert.Equal(2799m, listing.OldPrice);
        Assert.Equal(["21", "22", "23", "24", "25", "26"], listing.Sizes.Select(size => size.Label));
        Assert.Equal(Availability.InStock, listing.Availability);
    }

    [Fact]
    public void NonShoeProductYieldsNothing()
    {
        var raw = ProductRaw(Shops.SportM, "KQ6776", SportMProduct + "KQ6776", SampleFiles.SportMProductSocks);

        Assert.Empty(adapter.Adapt(raw, Shops.Context(Shops.SportM)));
    }

    [Fact]
    public void EmptyBreadcrumbIsUnknownSoTheShoeKeepsItsDetails()
    {
        var url = SportMProduct + "HQ8708";
        var raw = ProductRaw(Shops.SportM, "HQ8708", url, SampleFiles.SportMProductEmptyBreadcrumb);

        var entities = adapter.Adapt(raw, Shops.Context(Shops.SportM)).ToList();

        Assert.Equal(2, entities.Count);
        var product = Assert.IsType<Product>(entities[0]);
        var listing = Assert.IsType<Listing>(entities[1]);
        Assert.Equal("Adidas CAMPUS 00s", product.Name);
        Assert.Equal("HQ8708", product.Sku);
        Assert.Null(product.CategoryId);
        Assert.Equal(7399m, listing.Price);
        Assert.Equal(Shops.HarvestedAt, listing.InfoCheckedAt);
    }

    [Fact]
    public void BreadcrumbFollowsTheUrlContextNotTheProduct()
    {
        var socks = ProductPageParser.Parse(SampleFiles.Text(SampleFiles.SportMProductSocksShoeContext))!;
        var terinda = ProductPageParser.Parse(SampleFiles.Text(SampleFiles.IntersportProductSocksShoeContext))!;

        Assert.Equal("KQ6776", socks.ProductId);
        Assert.Equal(["Мажи", "Обувки"], socks.Crumbs.Select(crumb => crumb.Text));
        Assert.Equal("1304-BLK", terinda.ProductId);
        Assert.Equal("Terinda Sport socks FOTTSY", terinda.Name);
        Assert.Equal(["МАЖИ", "Обувки"], terinda.Crumbs.Select(crumb => crumb.Text));
    }

    [Fact]
    public void NonShoePageFetchedWithShoeContextYieldsNothing()
    {
        var url = SportMProduct + "KQ6776&Segment=03&Grupa=01";
        var raw = ProductRaw(Shops.SportM, "KQ6776", url, SampleFiles.SportMProductSocksShoeContext);

        Assert.Empty(adapter.Adapt(raw, Shops.Context(Shops.SportM)));
    }

    [Fact]
    public void IntersportNonShoePageFetchedWithShoeContextYieldsNothing()
    {
        var url = IntersportProduct + "1304-BLK&Segment=28&Grupa=01";
        var raw = ProductRaw(Shops.Intersport, "1304-BLK", url, SampleFiles.IntersportProductSocksShoeContext);

        Assert.Empty(adapter.Adapt(raw, Shops.Context(Shops.Intersport)));
    }

    [Theory]
    [InlineData("&Segment=03&Grupa=01")]
    [InlineData("&PodGrupa=514")]
    [InlineData("&Vozrast=A")]
    public void ShoePageFetchedWithAnyContextYieldsNothing(string context)
    {
        var url = SportMProduct + "IH7234" + context;
        var raw = ProductRaw(Shops.SportM, "IH7234", url, SampleFiles.SportMProductIH7234);

        Assert.Empty(adapter.Adapt(raw, Shops.Context(Shops.SportM)));
    }

    [Fact]
    public void PageWithoutProductYieldsNothing()
    {
        var url = SportMProduct + "ZZ0000000";
        var raw = ProductRaw(Shops.SportM, "ZZ0000000", url, SampleFiles.SportMProductMissing);

        Assert.Empty(adapter.Adapt(raw, Shops.Context(Shops.SportM)));
    }

    [Fact]
    public void PageOfAnotherProductYieldsNothing()
    {
        var raw = ProductRaw(Shops.SportM, "GW4604", SportMProduct + "GW4604", SampleFiles.SportMProductIH7234);

        Assert.Empty(adapter.Adapt(raw, Shops.Context(Shops.SportM)));
    }

    [Fact]
    public void ProductPageAndTileShareProductAndListingIds()
    {
        var context = Shops.Context(Shops.Intersport);
        var pageUrl = IntersportProduct + "JQ6920";
        var page = ProductRaw(Shops.Intersport, "JQ6920", pageUrl, SampleFiles.IntersportProductJQ6920);
        var catalogUrl = "https://intersport.mk/ProductCatalog?Segment=28&Grupa=01&Page=1";
        var catalogId = "Segment=28&Grupa=01/page-1";
        var sample = SampleFiles.IntersportMenShoesPage1;
        var tiles = Shops.Raw(Shops.Intersport, "catalog-page", catalogId, catalogUrl, sample);

        var fromPage = adapter.Adapt(page, context).Where(entity => entity is not Category).Select(e => e.Id);
        var fromTiles = adapter.Adapt(tiles, context).Where(entity => entity.ProviderId == "JQ6920").Select(e => e.Id);

        Assert.Equal(fromTiles, fromPage);
    }

    private static RawItem ProductRaw(Source source, string providerId, string url, string sample)
    {
        return Shops.Raw(source, "product-page", providerId, url, sample);
    }
}
