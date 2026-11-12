using Collector.Core.Messages;
using Collector.Harvesting;

namespace Collector.Providers.Nbshop.Tests;

public class NbshopProductPageCommandTests
{
    private const string ConverseUrl =
        "https://www.sportvision.mk/mk/patiki/8733931-converse-chuck-taylor-all-star-equip";
    private const string VsPaceUrl = "https://www.sportvision.mk/mk/patiki/8566650-adidas-vs-pace-2-0";
    private const string GoneUrl = "https://www.sportvision.mk/mk/patiki/8500001-gone";

    private readonly NbshopProductPageCommand command = new(new FixedClock(TestShops.Now));

    [Fact]
    public async Task Every_target_gives_one_item_and_failures_stay_per_target()
    {
        var fetcher = new FakeFetcher()
            .Serve(ConverseUrl, SampleFiles.SportVisionConverseShoe)
            .Serve(VsPaceUrl, SampleFiles.SportVisionVsPace);
        var request = Targets(
            new HarvestTarget { ProviderId = "8733931", Url = ConverseUrl },
            new HarvestTarget { ProviderId = "8500001", Url = GoneUrl },
            new HarvestTarget { ProviderId = "8566650", Url = VsPaceUrl });

        var items = await Run(request, fetcher);

        Assert.Equal([ConverseUrl, GoneUrl, VsPaceUrl], fetcher.Requests);
        Assert.Equal(["8733931", "8500001", "8566650"], items.Select(item => item.ProviderId));
        Assert.All(items, item => Assert.Equal("product-page", item.Kind));
        Assert.All(items, item => Assert.Equal(TestShops.Now.UtcDateTime, item.HarvestedAt));
        Assert.Equal(SampleFiles.Bytes(SampleFiles.SportVisionConverseShoe), items[0].Body);
        Assert.Equal(ConverseUrl, items[0].Url);
        Assert.Equal("http-404", items[1].ErrorClass);
        Assert.Equal(GoneUrl, items[1].Url);
        Assert.False(items[2].Failed);
    }

    [Fact]
    public async Task The_limit_caps_the_number_of_targets()
    {
        var fetcher = new FakeFetcher().Serve(ConverseUrl, SampleFiles.SportVisionConverseShoe);
        var request = Targets(
            new HarvestTarget { ProviderId = "8733931", Url = ConverseUrl },
            new HarvestTarget { ProviderId = "8566650", Url = VsPaceUrl }) with { Limit = 1 };

        var items = await Run(request, fetcher);

        Assert.Equal([ConverseUrl], fetcher.Requests);
        Assert.Equal("8733931", Assert.Single(items).ProviderId);
    }

    [Fact]
    public async Task A_relative_url_is_resolved_against_the_source_and_the_id_is_read_from_it()
    {
        var fetcher = new FakeFetcher().Serve(VsPaceUrl, SampleFiles.SportVisionVsPace);
        var request = Targets(new HarvestTarget { Url = "/mk/patiki/8566650-adidas-vs-pace-2-0" });

        var items = await Run(request, fetcher);

        Assert.Equal([VsPaceUrl], fetcher.Requests);
        Assert.Equal("8566650", Assert.Single(items).ProviderId);
    }

    [Fact]
    public async Task A_target_without_url_is_a_failed_item_and_fetches_nothing()
    {
        var fetcher = new FakeFetcher();

        var items = await Run(Targets(new HarvestTarget { ProviderId = "8733931" }), fetcher);

        var item = Assert.Single(items);
        Assert.Equal("missing-url", item.ErrorClass);
        Assert.Equal("8733931", item.ProviderId);
        Assert.Empty(fetcher.Requests);
    }

    [Fact]
    public async Task A_fetcher_exception_fails_only_its_own_target()
    {
        var fetcher = new FakeFetcher()
            .Fail(ConverseUrl, new TaskCanceledException("timeout"))
            .Serve(VsPaceUrl, SampleFiles.SportVisionVsPace);
        var request = Targets(
            new HarvestTarget { ProviderId = "8733931", Url = ConverseUrl },
            new HarvestTarget { ProviderId = "8566650", Url = VsPaceUrl });

        var items = await Run(request, fetcher);

        Assert.Equal("TaskCanceledException", items[0].ErrorClass);
        Assert.False(items[1].Failed);
    }

    [Fact]
    public async Task A_robots_refusal_is_a_robots_failure_of_its_own_target()
    {
        var refusal = new RobotsDisallowedException(new Uri(ConverseUrl));
        var fetcher = new FakeFetcher()
            .Fail(ConverseUrl, refusal)
            .Serve(VsPaceUrl, SampleFiles.SportVisionVsPace);
        var request = Targets(
            new HarvestTarget { ProviderId = "8733931", Url = ConverseUrl },
            new HarvestTarget { ProviderId = "8566650", Url = VsPaceUrl });

        var items = await Run(request, fetcher);

        Assert.Equal("robots", items[0].ErrorClass);
        Assert.Equal(refusal.Message, items[0].ErrorText);
        Assert.Equal("8733931", items[0].ProviderId);
        Assert.Equal(ConverseUrl, items[0].Url);
        Assert.False(items[1].Failed);
    }

    private static HarvestRequest Targets(params HarvestTarget[] targets) =>
        TestShops.Request(TestShops.SportVision) with { Targets = [.. targets] };

    private Task<List<HarvestItem>> Run(HarvestRequest request, FakeFetcher fetcher) =>
        TestShops.Collect(command.RunAsync(request, fetcher, CancellationToken.None));
}
