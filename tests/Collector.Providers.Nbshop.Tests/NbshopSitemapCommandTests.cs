using Collector.Harvesting;

namespace Collector.Providers.Nbshop.Tests;

public class NbshopSitemapCommandTests
{
    private const string SitemapUrl = "https://www.sportvision.mk/files/sitemap/MAK_mk/product.xml";

    private readonly NbshopSitemapCommand command = new(new FixedClock(TestShops.Now));

    [Fact]
    public async Task Fetches_the_product_sitemap_of_the_source_region()
    {
        var fetcher = new FakeFetcher().Serve(SitemapUrl, SampleFiles.SportVisionSitemap, "text/xml");

        var items = await Run(TestShops.Request(TestShops.SportVision), fetcher);

        var item = Assert.Single(items);
        Assert.Equal([SitemapUrl], fetcher.Requests);
        Assert.False(item.Failed);
        Assert.Equal("sitemap", item.Kind);
        Assert.Equal("product-sitemap", item.ProviderId);
        Assert.Equal(SitemapUrl, item.Url);
        Assert.Equal("text/xml", item.ContentType);
        Assert.Equal(SampleFiles.Bytes(SampleFiles.SportVisionSitemap), item.Body);
        Assert.Equal(TestShops.Now.UtcDateTime, item.HarvestedAt);
    }

    [Fact]
    public async Task A_server_error_is_a_failed_item()
    {
        var fetcher = new FakeFetcher().Answer(SitemapUrl, 503);

        var items = await Run(TestShops.Request(TestShops.SportVision), fetcher);

        var item = Assert.Single(items);
        Assert.Equal("http-503", item.ErrorClass);
        Assert.Equal("product-sitemap", item.ProviderId);
        Assert.Equal(SitemapUrl, item.Url);
        Assert.Empty(item.Body);
    }

    [Fact]
    public async Task A_fetcher_exception_is_a_failed_item_named_after_the_exception()
    {
        var fetcher = new FakeFetcher().Fail(SitemapUrl, new HttpRequestException("Connection reset by peer"));

        var items = await Run(TestShops.Request(TestShops.SportVision), fetcher);

        var item = Assert.Single(items);
        Assert.Equal("HttpRequestException", item.ErrorClass);
        Assert.Equal("Connection reset by peer", item.ErrorText);
    }

    [Fact]
    public async Task A_robots_txt_that_shuts_the_host_is_a_robots_failure()
    {
        var message = "robots.txt of www.sportvision.mk answered 503, the host is treated as disallowed";
        var refusal = new RobotsDisallowedException(new Uri(SitemapUrl), message);
        var fetcher = new FakeFetcher().Fail(SitemapUrl, refusal);

        var items = await Run(TestShops.Request(TestShops.SportVision), fetcher);

        var item = Assert.Single(items);
        Assert.Equal("robots", item.ErrorClass);
        Assert.Equal(message, item.ErrorText);
        Assert.Equal("product-sitemap", item.ProviderId);
        Assert.Equal(SitemapUrl, item.Url);
    }

    [Fact]
    public async Task A_source_without_a_sitemap_region_is_a_failed_item_and_fetches_nothing()
    {
        var source = TestShops.SportVision with { Settings = [] };
        var fetcher = new FakeFetcher();

        var items = await Run(TestShops.Request(source), fetcher);

        var item = Assert.Single(items);
        Assert.Equal("missing-setting", item.ErrorClass);
        Assert.Empty(fetcher.Requests);
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var fetcher = new FakeFetcher().Fail(SitemapUrl, new OperationCanceledException(cancellation.Token));

        var run = command.RunAsync(TestShops.Request(TestShops.SportVision), fetcher, cancellation.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() => TestShops.Collect(run));
    }

    private Task<List<HarvestItem>> Run(HarvestRequest request, FakeFetcher fetcher) =>
        TestShops.Collect(command.RunAsync(request, fetcher, CancellationToken.None));
}
