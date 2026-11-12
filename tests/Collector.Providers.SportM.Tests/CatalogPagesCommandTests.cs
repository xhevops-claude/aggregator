using Collector.Harvesting;

namespace Collector.Providers.SportM.Tests;

public sealed class CatalogPagesCommandTests
{
    private const string SportMCatalog = "https://sport-m.com.mk/ProductCatalog?";
    private const string IntersportCatalog = "https://intersport.mk/ProductCatalog?";

    private readonly SportMCatalogPagesCommand command = new(new FixedTime());

    [Fact]
    public void NameAndPlatform()
    {
        Assert.Equal("sportm.catalog-pages", command.Name);
        Assert.Equal("sportm", command.Platform);
    }

    [Fact]
    public async Task WithoutListingWalksTheShoeSegmentsUntilAPageHasNoNewTile()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMCatalog + "Segment=03&Grupa=01&Page=1", SampleFiles.SportMMenShoesPage1);
        fetcher.Serve(SportMCatalog + "Segment=03&Grupa=01&Page=2", SampleFiles.SportMMenShoesNikePage1);
        fetcher.Serve(SportMCatalog + "Segment=03&Grupa=01&Page=3", SampleFiles.SportMMenShoesPage1);
        fetcher.Serve(SportMCatalog + "Segment=04&Grupa=01&Page=1", SampleFiles.SportMMenShoesPage400);
        fetcher.Serve(SportMCatalog + "Segment=05&Grupa=01&Page=1", SampleFiles.SportMMenShoesPage400);

        var items = await HarvestRuns.CollectAsync(command, Shops.Request(Shops.SportM), fetcher);

        string[] expected =
        [
            "Segment=03&Grupa=01/page-1",
            "Segment=03&Grupa=01/page-2",
            "Segment=03&Grupa=01/page-3",
            "Segment=04&Grupa=01/page-1",
            "Segment=05&Grupa=01/page-1",
        ];
        Assert.Equal(expected, items.Select(item => item.ProviderId));
        Assert.All(items, item => Assert.False(item.Failed));
        Assert.All(items, item => Assert.Equal("catalog-page", item.Kind));
        Assert.All(items, item => Assert.Equal(FixedTime.Now, item.HarvestedAt));
        Assert.Equal(5, fetcher.Requested.Count);
    }

    [Fact]
    public async Task StoredPageKeepsUrlBodyAndContentType()
    {
        var url = SportMCatalog + "Segment=03&Grupa=01&Page=1";
        var fetcher = new SampleFetcher();
        fetcher.Serve(url, SampleFiles.SportMMenShoesPage1);
        var request = Shops.Request(Shops.SportM) with { Limit = 1 };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        var item = Assert.Single(items);
        Assert.Equal(url, item.Url);
        Assert.Equal("text/html; charset=utf-8", item.ContentType);
        Assert.Equal(SampleFiles.Bytes(SampleFiles.SportMMenShoesPage1), item.Body);
        Assert.Null(item.ErrorClass);
    }

    [Fact]
    public async Task IntersportUsesItsOwnSegmentsAndA404EndsTheWalk()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(IntersportCatalog + "Segment=28&Grupa=01&Page=1", SampleFiles.IntersportMenShoesPage1);
        fetcher.Serve(IntersportCatalog + "Segment=28&Grupa=01&Page=2", SampleFiles.IntersportMenShoesPage33);

        var items = await HarvestRuns.CollectAsync(command, Shops.Request(Shops.Intersport), fetcher);

        string[] expected =
        [
            "Segment=28&Grupa=01/page-1",
            "Segment=28&Grupa=01/page-2",
            "Segment=29&Grupa=01/page-1",
            "Segment=30&Grupa=01/page-1",
        ];
        Assert.Equal(expected, items.Select(item => item.ProviderId));
        Assert.Equal([null, null, "http-404", "http-404"], items.Select(item => item.ErrorClass));
        Assert.Equal(IntersportCatalog + "Segment=29&Grupa=01&Page=1", items[2].Url);
        Assert.Empty(items[2].Body);
        Assert.Equal(4, fetcher.Requested.Count);
    }

    [Fact]
    public async Task BrandParameterAddsTheBrendCodeToEveryShoeListing()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMCatalog + "Segment=03&Grupa=01&Brend=02&Page=1", SampleFiles.SportMMenShoesNikePage1);
        fetcher.Serve(SportMCatalog + "Segment=03&Grupa=01&Brend=02&Page=2", SampleFiles.SportMMenShoesPage400);
        fetcher.Serve(SportMCatalog + "Segment=04&Grupa=01&Brend=02&Page=1", SampleFiles.SportMMenShoesPage400);
        fetcher.Serve(SportMCatalog + "Segment=05&Grupa=01&Brend=02&Page=1", SampleFiles.SportMMenShoesPage400);
        var request = Shops.Request(Shops.SportM) with { Parameters = new() { ["brand"] = "NIKE" } };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        string[] expected =
        [
            "Segment=03&Grupa=01&Brend=02/page-1",
            "Segment=03&Grupa=01&Brend=02/page-2",
            "Segment=04&Grupa=01&Brend=02/page-1",
            "Segment=05&Grupa=01&Brend=02/page-1",
        ];
        Assert.Equal(expected, items.Select(item => item.ProviderId));
        Assert.All(items, item => Assert.False(item.Failed));
    }

    [Fact]
    public async Task ListingParameterIsUsedAsGivenWithoutItsPage()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMCatalog + "Segment=17&Grupa=103&Popust=True&Page=1", SampleFiles.SportMMenShoesNikePage1);
        fetcher.Serve(SportMCatalog + "Segment=17&Grupa=103&Popust=True&Page=2", SampleFiles.SportMMenShoesPage400);
        fetcher.Serve(SportMCatalog + "Segment=04&Grupa=01&Page=1", SampleFiles.SportMMenShoesPage400);
        var listing = "?Segment=17&Grupa=103&Page=7&Popust=True | Segment=04&Grupa=01";
        var request = Shops.Request(Shops.SportM) with { Parameters = new() { ["listing"] = listing } };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        string[] expected =
        [
            "Segment=17&Grupa=103&Popust=True/page-1",
            "Segment=17&Grupa=103&Popust=True/page-2",
            "Segment=04&Grupa=01/page-1",
        ];
        Assert.Equal(expected, items.Select(item => item.ProviderId));
    }

    [Fact]
    public async Task ListingAndBrandCombine()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMCatalog + "Segment=03&Grupa=01&Brend=00050&Page=1", SampleFiles.SportMMenShoesPage400);
        var parameters = new Dictionary<string, string> { ["listing"] = "Segment=03&Grupa=01", ["brand"] = "crocs" };
        var request = Shops.Request(Shops.SportM) with { Parameters = parameters };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        var item = Assert.Single(items);
        Assert.Equal("Segment=03&Grupa=01&Brend=00050/page-1", item.ProviderId);
        Assert.Equal(SportMCatalog + "Segment=03&Grupa=01&Brend=00050&Page=1", item.Url);
    }

    [Fact]
    public async Task LimitCapsTheNumberOfPagesAcrossListings()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMCatalog + "Segment=03&Grupa=01&Page=1", SampleFiles.SportMMenShoesPage1);
        fetcher.Serve(SportMCatalog + "Segment=03&Grupa=01&Page=2", SampleFiles.SportMMenShoesNikePage1);
        var request = Shops.Request(Shops.SportM) with { Limit = 2 };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        Assert.Equal(2, items.Count);
        Assert.Equal(2, fetcher.Requested.Count);
        Assert.Equal("Segment=03&Grupa=01/page-2", items[1].ProviderId);
    }

    [Fact]
    public async Task UnknownBrandYieldsOneFailedItemAndFetchesNothing()
    {
        var fetcher = new SampleFetcher();
        var request = Shops.Request(Shops.SportM) with { Parameters = new() { ["brand"] = "Lotto" } };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        var item = Assert.Single(items);
        Assert.Equal("unknown-brand", item.ErrorClass);
        Assert.Equal("brand=Lotto", item.ProviderId);
        Assert.Equal("catalog-page", item.Kind);
        Assert.Empty(fetcher.Requested);
    }

    [Fact]
    public async Task NumericBrandIsUsedAsABrendCode()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMCatalog + "Segment=03&Grupa=01&Brend=132&Page=1", SampleFiles.SportMMenShoesPage400);
        var parameters = new Dictionary<string, string> { ["listing"] = "Segment=03&Grupa=01", ["brand"] = "132" };
        var request = Shops.Request(Shops.SportM) with { Parameters = parameters };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        Assert.Equal("Segment=03&Grupa=01&Brend=132/page-1", Assert.Single(items).ProviderId);
    }

    [Fact]
    public async Task UnknownProviderWithoutListingYieldsOneFailedItem()
    {
        var source = Shops.SportM with { Provider = "elsewhere" };
        var fetcher = new SampleFetcher();

        var items = await HarvestRuns.CollectAsync(command, Shops.Request(source), fetcher);

        Assert.Equal("no-listing", Assert.Single(items).ErrorClass);
        Assert.Empty(fetcher.Requested);
    }

    [Fact]
    public async Task SourceSettingOverridesTheDefaultShoeListings()
    {
        var settings = new Dictionary<string, string> { ["shoeListings"] = "Segment=05&Grupa=01" };
        var source = Shops.SportM with { Settings = settings };
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMCatalog + "Segment=05&Grupa=01&Page=1", SampleFiles.SportMMenShoesPage400);

        var items = await HarvestRuns.CollectAsync(command, Shops.Request(source), fetcher);

        Assert.Equal("Segment=05&Grupa=01/page-1", Assert.Single(items).ProviderId);
    }

    [Fact]
    public async Task ThreeFailuresInARowEndTheWalk()
    {
        var fetcher = new SampleFetcher { FailEverything = new HttpRequestException("connection reset") };
        var request = Shops.Request(Shops.SportM) with { Parameters = new() { ["listing"] = "Segment=03&Grupa=01" } };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        Assert.Equal(3, items.Count);
        Assert.All(items, item => Assert.Equal("HttpRequestException", item.ErrorClass));
        Assert.All(items, item => Assert.Equal("connection reset", item.ErrorText));
        Assert.Equal("Segment=03&Grupa=01/page-3", items[2].ProviderId);
    }

    [Fact]
    public async Task ServerErrorIsAFailedPageAndTheWalkGoesOn()
    {
        var fetcher = new SampleFetcher();
        fetcher.Status(SportMCatalog + "Segment=03&Grupa=01&Page=1", 503);
        fetcher.Serve(SportMCatalog + "Segment=03&Grupa=01&Page=2", SampleFiles.SportMMenShoesPage400);
        var request = Shops.Request(Shops.SportM) with { Parameters = new() { ["listing"] = "Segment=03&Grupa=01" } };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        Assert.Equal(["http-503", null], items.Select(item => item.ErrorClass));
    }

    [Fact]
    public async Task RobotsRefusalEndsTheWalk()
    {
        var url = SportMCatalog + "Segment=03&Grupa=01&Page=1";
        var fetcher = new SampleFetcher();
        fetcher.Throw(url, new RobotsDisallowedException(url));
        var request = Shops.Request(Shops.SportM) with { Parameters = new() { ["listing"] = "Segment=03&Grupa=01" } };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        var item = Assert.Single(items);
        Assert.Equal("robots", item.ErrorClass);
        Assert.True(item.Failed);
    }

    [Fact]
    public async Task CancellationIsNotSwallowed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var fetcher = new SampleFetcher { FailEverything = new OperationCanceledException(cancellation.Token) };
        var request = Shops.Request(Shops.SportM);

        var run = HarvestRuns.CollectAsync(command, request, fetcher, cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => run);
    }

    private sealed class RobotsDisallowedException(string url) : Exception($"robots.txt disallows {url}");
}
