using Collector.Harvesting;

namespace Collector.Providers.Nbshop.Tests;

public class NbshopListingPagesCommandTests
{
    private const string Page1 = "https://www.sportvision.mk/mk/proizvodi/converse";
    private const string Page2 = "https://www.sportvision.mk/mk/proizvodi/converse/page-2";
    private const string Page3 = "https://www.sportvision.mk/mk/proizvodi/converse/page-3";

    private readonly NbshopListingPagesCommand command = new(new FixedClock(TestShops.Now));

    [Fact]
    public async Task A_brand_walks_its_proizvodi_listing_until_a_page_answers_404()
    {
        var fetcher = new FakeFetcher()
            .Serve(Page1, SampleFiles.SportVisionConversePage1)
            .Serve(Page2, SampleFiles.SportVisionConversePage4);

        var items = await Run(Brand("Converse"), fetcher);

        Assert.Equal([Page1, Page2, Page3], fetcher.Requests);
        Assert.Equal(["proizvodi/converse/page-1", "proizvodi/converse/page-2"], items.Select(item => item.ProviderId));
        Assert.All(items, item => Assert.False(item.Failed));
        Assert.All(items, item => Assert.Equal("listing-page", item.Kind));
        Assert.All(items, item => Assert.Equal(TestShops.Now.UtcDateTime, item.HarvestedAt));
        Assert.Equal(Page1, items[0].Url);
        Assert.Equal(SampleFiles.Bytes(SampleFiles.SportVisionConversePage1), items[0].Body);
    }

    [Fact]
    public async Task The_walk_stops_on_a_page_without_new_tiles_and_drops_that_page()
    {
        var fetcher = new FakeFetcher()
            .Serve(Page1, SampleFiles.SportVisionConversePage1)
            .Serve(Page2, SampleFiles.SportVisionConversePage1);

        var items = await Run(Listing("proizvodi/converse"), fetcher);

        Assert.Equal([Page1, Page2], fetcher.Requests);
        Assert.Equal("proizvodi/converse/page-1", Assert.Single(items).ProviderId);
    }

    [Fact]
    public async Task The_walk_stops_on_a_page_without_a_link_to_the_next_page()
    {
        var fetcher = new FakeFetcher().Serve(Page1, SampleFiles.SportVisionConversePage4);

        var items = await Run(Listing("/proizvodi/converse/"), fetcher);

        Assert.Equal([Page1], fetcher.Requests);
        Assert.Equal("proizvodi/converse/page-1", Assert.Single(items).ProviderId);
    }

    [Fact]
    public async Task The_limit_caps_the_number_of_pages()
    {
        var fetcher = new FakeFetcher()
            .Serve(Page1, SampleFiles.SportVisionConversePage1)
            .Serve(Page2, SampleFiles.SportVisionConversePage4);

        var items = await Run(Brand("Converse") with { Limit = 1 }, fetcher);

        Assert.Equal([Page1], fetcher.Requests);
        Assert.Single(items);
    }

    [Fact]
    public async Task A_first_page_answering_404_is_a_failed_item()
    {
        var fetcher = new FakeFetcher();

        var items = await Run(Brand("New Balance"), fetcher);

        var item = Assert.Single(items);
        Assert.Equal(["https://www.sportvision.mk/mk/proizvodi/new-balance"], fetcher.Requests);
        Assert.Equal("http-404", item.ErrorClass);
        Assert.Equal("proizvodi/new-balance/page-1", item.ProviderId);
        Assert.Equal("https://www.sportvision.mk/mk/proizvodi/new-balance", item.Url);
    }

    [Fact]
    public async Task A_failing_later_page_is_a_failed_item_and_ends_the_walk()
    {
        var fetcher = new FakeFetcher()
            .Serve(Page1, SampleFiles.SportVisionConversePage1)
            .Fail(Page2, new HttpRequestException("Connection reset by peer"));

        var items = await Run(Brand("Converse"), fetcher);

        Assert.Equal([Page1, Page2], fetcher.Requests);
        Assert.Equal(2, items.Count);
        Assert.False(items[0].Failed);
        Assert.Equal("HttpRequestException", items[1].ErrorClass);
        Assert.Equal("proizvodi/converse/page-2", items[1].ProviderId);
    }

    [Fact]
    public async Task A_request_without_listing_or_brand_is_a_failed_item_and_fetches_nothing()
    {
        var fetcher = new FakeFetcher();

        var items = await Run(TestShops.Request(TestShops.SportVision), fetcher);

        Assert.Equal("missing-parameter", Assert.Single(items).ErrorClass);
        Assert.Empty(fetcher.Requests);
    }

    [Fact]
    public async Task A_listing_with_a_query_follows_the_platform_next_link_form()
    {
        const string first = "https://www.sportreality.mk/mk/proizvodi/?outlet=yes";
        var fetcher = new FakeFetcher().Serve(first, SampleFiles.SportRealityOutletPage1);
        var request = TestShops.Request(TestShops.SportReality) with
        {
            Parameters = new Dictionary<string, string> { [HarvestParameters.Listing] = "proizvodi/?outlet=yes" },
        };

        var items = await Run(request, fetcher);

        Assert.Equal([first, first + "/page-2"], fetcher.Requests);
        Assert.Equal("proizvodi/?outlet=yes/page-1", Assert.Single(items).ProviderId);
    }

    private static HarvestRequest Brand(string brand) => TestShops.Request(TestShops.SportVision) with
    {
        Parameters = new Dictionary<string, string> { [HarvestParameters.Brand] = brand },
    };

    private static HarvestRequest Listing(string listing) => TestShops.Request(TestShops.SportVision) with
    {
        Parameters = new Dictionary<string, string> { [HarvestParameters.Listing] = listing },
    };

    private Task<List<HarvestItem>> Run(HarvestRequest request, FakeFetcher fetcher) =>
        TestShops.Collect(command.RunAsync(request, fetcher, CancellationToken.None));
}
