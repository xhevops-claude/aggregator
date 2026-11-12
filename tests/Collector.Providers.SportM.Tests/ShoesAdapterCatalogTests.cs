using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Core.Platform;
using Collector.Lake;

namespace Collector.Providers.SportM.Tests;

public sealed class ShoesAdapterCatalogTests
{
    private const string SportMCatalog = "https://sport-m.com.mk/ProductCatalog?";
    private const string IntersportCatalog = "https://intersport.mk/ProductCatalog?";

    private readonly SportMShoesAdapter adapter = new();

    [Fact]
    public void DescribesItself()
    {
        Assert.Equal("sportm.shoes", adapter.Name);
        Assert.Equal("sportm", adapter.Platform);
        Assert.Equal("shoes", adapter.Domain);
        Assert.Equal(["catalog-page", "product-page"], adapter.RawKinds);
    }

    [Fact]
    public void SportMTileBecomesAProductCandidateAndAListing()
    {
        var url = SportMCatalog + "Segment=03&Grupa=01&Page=1";
        var raw = Catalog(Shops.SportM, "Segment=03&Grupa=01/page-1", url, SampleFiles.SportMMenShoesPage1);

        var entities = adapter.Adapt(raw, Shops.Context(Shops.SportM)).ToList();

        Assert.Equal(36, entities.Count);
        var product = Assert.IsType<Product>(entities[0]);
        var listing = Assert.IsType<Listing>(entities[1]);
        var productId = ProductIdentity.Candidate(Shops.Seed, "sportm", "IH7234");

        Assert.Equal(productId, product.Id);
        Assert.Equal(Shops.ProjectId, product.ProjectId);
        Assert.Equal("clothing/shoes/sportm", product.Namespace);
        Assert.Equal("sportm", product.Provider);
        Assert.Equal("IH7234", product.ProviderId);
        Assert.Equal(url, product.SourceUrl);
        Assert.Equal(Shops.HarvestedAt, product.HarvestedAt);
        Assert.Equal("Adidas PREDATOR PRO FT FG", product.Name);
        Assert.Equal("adidas", product.Brand);
        Assert.Equal("IH7234", product.Sku);
        Assert.Null(product.CategoryId);
        Assert.Equal(["https://sport-m.com.mk/ProductImages/IH7234.webp"], product.Images);

        Assert.Equal(DeterministicId.For(Shops.Seed, "sportm", "listing", "IH7234"), listing.Id);
        Assert.Equal(Shops.ProjectId, listing.ProjectId);
        Assert.Equal("clothing/shoes/sportm", listing.Namespace);
        Assert.Equal("sportm", listing.Provider);
        Assert.Equal("IH7234", listing.ProviderId);
        Assert.Equal(url, listing.SourceUrl);
        Assert.Equal(Shops.HarvestedAt, listing.HarvestedAt);
        Assert.Equal(productId, listing.ProductId);
        Assert.Equal("Adidas PREDATOR PRO FT FG", listing.Title);
        Assert.Equal(9899m, listing.Price);
        Assert.Null(listing.OldPrice);
        Assert.Equal("MKD", listing.Currency);
        Assert.Equal("https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=IH7234", listing.Url);
        Assert.Equal(Availability.Unknown, listing.Availability);
        Assert.Empty(listing.Sizes);
        Assert.Null(listing.InfoCheckedAt);
        Assert.Null(listing.StockCheckedAt);
    }

    [Fact]
    public void SportMTilesWithoutLogoTakeTheBrandFromTheName()
    {
        var url = SportMCatalog + "Segment=03&Grupa=01&Page=1";
        var raw = Catalog(Shops.SportM, "Segment=03&Grupa=01/page-1", url, SampleFiles.SportMMenShoesPage1);

        var products = adapter.Adapt(raw, Shops.Context(Shops.SportM)).OfType<Product>().ToList();

        var brands = products.ToDictionary(product => product.Sku!, product => product.Brand);
        Assert.Equal("Puma", brands["313739-01"]);
        Assert.Equal("Skechers", brands["118312-DKCC"]);
        Assert.Equal("adidas", brands["KJ1734"]);
        Assert.Equal(["Puma", "Skechers", "adidas"], brands.Values.Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SportMBrendFilterNamesTheBrandAndDiscountedTilesKeepBothPrices()
    {
        var url = SportMCatalog + "Segment=03&Grupa=01&Brend=02&Page=1";
        var providerId = "Segment=03&Grupa=01&Brend=02/page-1";
        var raw = Catalog(Shops.SportM, providerId, url, SampleFiles.SportMMenShoesNikePage1);

        var entities = adapter.Adapt(raw, Shops.Context(Shops.SportM)).ToList();

        var products = entities.OfType<Product>().ToList();
        var listings = entities.OfType<Listing>().ToDictionary(listing => listing.ProviderId);
        Assert.Equal(18, products.Count);
        Assert.All(products, product => Assert.Equal("Nike", product.Brand));

        Assert.Equal(5499m, listings["II7403-102"].Price);
        Assert.Null(listings["II7403-102"].OldPrice);
        Assert.Equal(3599m, listings["HJ9198-403"].Price);
        Assert.Equal(4499m, listings["HJ9198-403"].OldPrice);
        Assert.Equal(8392m, listings["FQ7939-061"].Price);
        Assert.Equal(10490m, listings["FQ7939-061"].OldPrice);
        Assert.Equal(6399m, listings["IR0951-001"].Price);
        Assert.Equal(7999m, listings["IR0951-001"].OldPrice);
        Assert.Equal(13, listings.Values.Count(listing => listing.OldPrice is not null));
        Assert.Equal("NIKE AIR FORCE 1 '07 LOW ESS", listings["FJ4146-129"].Title);
    }

    [Fact]
    public void IntersportTileTakesTheBrandFromTheLogo()
    {
        var url = IntersportCatalog + "Segment=28&Grupa=01&Page=1";
        var raw = Catalog(Shops.Intersport, "Segment=28&Grupa=01/page-1", url, SampleFiles.IntersportMenShoesPage1);

        var entities = adapter.Adapt(raw, Shops.Context(Shops.Intersport)).ToList();

        Assert.Equal(36, entities.Count);
        var product = Assert.IsType<Product>(entities[0]);
        var listing = Assert.IsType<Listing>(entities[1]);
        Assert.Equal(ProductIdentity.Candidate(Shops.Seed, "intersport", "IB2750-006"), product.Id);
        Assert.Equal("clothing/shoes/intersport", product.Namespace);
        Assert.Equal("intersport", product.Provider);
        Assert.Equal("NIKE CHARGE SUEDE", product.Name);
        Assert.Equal("Nike", product.Brand);
        Assert.Equal("IB2750-006", product.Sku);
        Assert.Equal(["https://intersport.mk/ProductImages/IB2750-006.webp"], product.Images);

        Assert.Equal(DeterministicId.For(Shops.Seed, "intersport", "listing", "IB2750-006"), listing.Id);
        Assert.Equal(product.Id, listing.ProductId);
        Assert.Equal(4499m, listing.Price);
        Assert.Null(listing.OldPrice);
        Assert.Equal("https://intersport.mk/ProductCatalog/ProductDetails?ProductID=IB2750-006", listing.Url);

        var brands = entities.OfType<Product>().ToDictionary(p => p.Sku!, p => p.Brand);
        Assert.Equal("McKinley", brands["430230-046-057"]);
        Assert.Equal("Salomon", brands["L47277000"]);
        Assert.Equal("On", brands["3MF10070692"]);
        Assert.Equal("Energetics", brands["434636-050-001"]);
    }

    [Fact]
    public void IntersportDiscountedTileReadsTheCartCall()
    {
        var url = IntersportCatalog + "Segment=35&Grupa=014&Popust=True&Page=1";
        var providerId = "Segment=35&Grupa=014&Popust=True/page-1";
        var raw = Catalog(Shops.Intersport, providerId, url, SampleFiles.IntersportSaleSneakersPage1);

        var entities = adapter.Adapt(raw, Shops.Context(Shops.Intersport)).ToList();

        var listings = entities.OfType<Listing>().ToDictionary(listing => listing.ProviderId);
        var brands = entities.OfType<Product>().ToDictionary(p => p.Sku!, p => p.Brand);
        Assert.Equal(18, listings.Count);
        Assert.Equal(2239m, listings["400603N-BKYL"].Price);
        Assert.Equal(2799m, listings["400603N-BKYL"].OldPrice);
        Assert.Equal(7912m, listings["L47181300"].Price);
        Assert.Equal(9890m, listings["L47181300"].OldPrice);
        Assert.Equal("Skechers VORTEX 2.0 - ZORENTO", listings["400603N-BKYL"].Title);
        Assert.Equal("Crocs", brands["206990-001"]);
        Assert.All(listings.Values, listing => Assert.NotNull(listing.OldPrice));
    }

    [Fact]
    public void MixedPageKeepsOnlyFootwearTiles()
    {
        var url = SportMCatalog + "Grupa=01&Page=1";
        var raw = Catalog(Shops.SportM, "Grupa=01/page-1", url, SampleFiles.SportMGroupOnlyMixedPage1);

        var entities = adapter.Adapt(raw, Shops.Context(Shops.SportM)).ToList();

        var skus = entities.OfType<Product>().Select(product => product.Sku);
        Assert.Equal(["JR4011", "JS3333", "ID1481", "JR0165", "KI3263-"], skus);
        Assert.Equal(10, entities.Count);
    }

    [Fact]
    public void PagePastTheEndYieldsNothing()
    {
        var url = SportMCatalog + "Segment=03&Grupa=01&Page=400";
        var raw = Catalog(Shops.SportM, "Segment=03&Grupa=01/page-400", url, SampleFiles.SportMMenShoesPage400);

        Assert.Empty(adapter.Adapt(raw, Shops.Context(Shops.SportM)));
    }

    [Fact]
    public void SameRawItemGivesTheSameEntities()
    {
        var url = IntersportCatalog + "Segment=28&Grupa=01&Page=1";
        var raw = Catalog(Shops.Intersport, "Segment=28&Grupa=01/page-1", url, SampleFiles.IntersportMenShoesPage1);
        var context = Shops.Context(Shops.Intersport);

        var first = adapter.Adapt(raw, context).Select(entity => entity.Id).ToList();
        var second = adapter.Adapt(raw, context).Select(entity => entity.Id).ToList();

        Assert.Equal(first, second);
        Assert.Equal(36, first.Distinct().Count());
    }

    [Fact]
    public void UnknownRawKindYieldsNothing()
    {
        var url = SportMCatalog + "Segment=03&Grupa=01&Page=1";
        var raw = Shops.Raw(Shops.SportM, "sitemap", "sitemap", url, SampleFiles.SportMMenShoesPage1);

        Assert.Empty(adapter.Adapt(raw, Shops.Context(Shops.SportM)));
    }

    private static RawItem Catalog(Source source, string providerId, string url, string sample)
    {
        return Shops.Raw(source, "catalog-page", providerId, url, sample);
    }
}
