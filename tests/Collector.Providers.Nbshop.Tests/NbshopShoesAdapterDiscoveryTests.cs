using System.Text;
using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Core.Platform;
using Collector.Lake;

namespace Collector.Providers.Nbshop.Tests;

public class NbshopShoesAdapterDiscoveryTests
{
    private const string SitemapUrl = "https://www.sportvision.mk/files/sitemap/MAK_mk/product.xml";
    private const string ConverseListing = "https://www.sportvision.mk/mk/proizvodi/converse";

    private readonly NbshopShoesAdapter adapter = new();

    [Fact]
    public void The_sitemap_gives_one_listing_per_shoe_url_and_ignores_the_rest()
    {
        var raw = SitemapRaw();

        var entities = adapter.Adapt(raw, TestShops.Context(TestShops.SportVision)).ToList();

        var listings = entities.Cast<Listing>().ToList();
        Assert.Equal(entities.Count, listings.Count);
        string[] expected =
        [
            "8738115", "8737719", "8737709", "8737704", "8737053", "8735756",
            "8733931", "8724789", "8722260", "8582940", "8566650",
        ];
        Assert.Equal(expected, listings.Select(listing => listing.ProviderId));
    }

    [Fact]
    public void A_sitemap_keeps_old_products_with_short_ids_and_drops_repeats()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
            <url><loc>https://www.buzzsneakers.mk/mk/patiki/108-adidas-patiki-gazelle</loc></url>
            <url><loc>https://www.buzzsneakers.mk/mk/corapi/4478-nike-corapi-value-cushioned-crew</loc></url>
            <url><loc>https://www.buzzsneakers.mk/mk/patiki/108-adidas-patiki-gazelle</loc></url>
            </urlset>
            """;
        var sitemap = SitemapRaw() with { Body = Encoding.UTF8.GetBytes(xml) };

        var listings = adapter.Adapt(sitemap, TestShops.Context(TestShops.Buzz)).Cast<Listing>().ToList();

        var listing = Assert.Single(listings);
        Assert.Equal("108", listing.ProviderId);
        Assert.Equal("adidas patiki gazelle", listing.Title);
        Assert.Equal(DeterministicId.For(TestShops.ProjectSeed, "buzzsneakers", "listing", "108"), listing.Id);
    }

    [Fact]
    public void A_sitemap_listing_carries_the_slug_title_and_our_ids()
    {
        var raw = SitemapRaw();

        var entities = adapter.Adapt(raw, TestShops.Context(TestShops.SportVision));

        var listing = entities.Cast<Listing>().Single(listing => listing.ProviderId == "8733931");
        var expected = new Listing
        {
            Id = DeterministicId.For(TestShops.ProjectSeed, "sportvision", "listing", "8733931"),
            ProjectId = TestShops.ProjectId,
            Namespace = "clothing/shoes/sportvision",
            Provider = "sportvision",
            ProviderId = "8733931",
            SourceUrl = SitemapUrl,
            HarvestedAt = TestShops.HarvestedAt,
            ProductId = ProductIdentity.Candidate(TestShops.ProjectSeed, "sportvision", "8733931"),
            Title = "converse chuck taylor all star equip",
            Url = "https://www.sportvision.mk/mk/patiki/8733931-converse-chuck-taylor-all-star-equip",
        };
        Assert.Equal(expected, listing with { Sizes = expected.Sizes });
        Assert.Empty(listing.Sizes);
    }

    [Fact]
    public void A_listing_page_gives_one_listing_per_shoe_tile()
    {
        var raw = ListingPage(TestShops.SportVision, ConverseListing, SampleFiles.SportVisionConversePage1);

        var listings = adapter.Adapt(raw, TestShops.Context(TestShops.SportVision)).Cast<Listing>().ToList();

        Assert.Equal(24, listings.Count);
        Assert.Equal(24, listings.Select(listing => listing.Id).Distinct().Count());
        var first = listings[0];
        var expected = new Listing
        {
            Id = DeterministicId.For(TestShops.ProjectSeed, "sportvision", "listing", "8733931"),
            ProjectId = TestShops.ProjectId,
            Namespace = "clothing/shoes/sportvision",
            Provider = "sportvision",
            ProviderId = "8733931",
            SourceUrl = ConverseListing,
            HarvestedAt = TestShops.HarvestedAt,
            ProductId = ProductIdentity.Candidate(TestShops.ProjectSeed, "sportvision", "8733931"),
            Title = "Converse Chuck Taylor All Star Equip",
            Url = "https://www.sportvision.mk/mk/patiki/8733931-converse-chuck-taylor-all-star-equip",
        };
        Assert.Equal(expected, first with { Sizes = expected.Sizes });
        Assert.Equal("8574641", listings[^1].ProviderId);
        Assert.Equal("Converse Chuck Taylor All Star Construct", listings[^1].Title);
    }

    [Fact]
    public void A_listing_page_of_clothes_gives_nothing()
    {
        var url = ConverseListing + "/page-4";
        var raw = ListingPage(TestShops.SportVision, url, SampleFiles.SportVisionConversePage4);

        var entities = adapter.Adapt(raw, TestShops.Context(TestShops.SportVision));

        Assert.Empty(entities);
    }

    [Fact]
    public void A_buzz_listing_page_keeps_the_cyrillic_tile_titles()
    {
        var url = "https://www.buzzsneakers.mk/mk/proizvodi/converse/page-2";
        var raw = ListingPage(TestShops.Buzz, url, SampleFiles.BuzzConversePage2);

        var listings = adapter.Adapt(raw, TestShops.Context(TestShops.Buzz)).Cast<Listing>().ToList();

        Assert.Equal(24, listings.Count);
        Assert.Equal("365956", listings[0].ProviderId);
        Assert.Equal("Converse Патики Chuck 70", listings[0].Title);
        Assert.Equal("https://www.buzzsneakers.mk/mk/patiki/365956-converse-patiki-chuck-70", listings[0].Url);
        Assert.Equal("buzzsneakers", listings[0].Provider);
        Assert.Equal(DeterministicId.For(TestShops.ProjectSeed, "buzzsneakers", "listing", "365956"), listings[0].Id);
    }

    [Fact]
    public void A_sport_reality_outlet_page_gives_its_slippers()
    {
        var url = "https://www.sportreality.mk/mk/proizvodi/?outlet=yes";
        var raw = ListingPage(TestShops.SportReality, url, SampleFiles.SportRealityOutletPage1);

        var listings = adapter.Adapt(raw, TestShops.Context(TestShops.SportReality)).Cast<Listing>().ToList();

        Assert.Equal(24, listings.Count);
        Assert.Equal("58593", listings[0].ProviderId);
        Assert.Equal("Kronos Angela", listings[0].Title);
        Assert.All(listings, listing => Assert.Contains("/mk/papuci/", listing.Url));
    }

    private static RawItem SitemapRaw() =>
        TestShops.Raw(TestShops.SportVision, "sitemap", "product-sitemap", SitemapUrl, SampleFiles.SportVisionSitemap);

    private static RawItem ListingPage(Source source, string url, string sample)
    {
        var providerId = new Uri(url).PathAndQuery.TrimStart('/');
        return TestShops.Raw(source, "listing-page", providerId, url, sample);
    }
}
