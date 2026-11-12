using Collector.Core.Messages;

namespace Collector.Providers.SportM.Tests;

public sealed class ProductPageCommandTests
{
    private const string SportMProduct = "https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=";
    private const string IntersportProduct = "https://intersport.mk/ProductCatalog/ProductDetails?ProductID=";

    private readonly SportMProductPageCommand command = new(new FixedTime());

    [Fact]
    public void NameAndPlatform()
    {
        Assert.Equal("sportm.product-page", command.Name);
        Assert.Equal("sportm", command.Platform);
    }

    [Fact]
    public async Task BuildsTheProductUrlWhenTheTargetHasNone()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMProduct + "IH7234", SampleFiles.SportMProductIH7234);
        fetcher.Serve(SportMProduct + "IB2750-001", SampleFiles.SportMProductIB2750);
        var request = Shops.Request(Shops.SportM) with { Targets = [Target("IH7234"), Target("IB2750-001")] };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        Assert.Equal(["IH7234", "IB2750-001"], items.Select(item => item.ProviderId));
        Assert.Equal([SportMProduct + "IH7234", SportMProduct + "IB2750-001"], items.Select(item => item.Url));
        Assert.All(items, item => Assert.Equal("product-page", item.Kind));
        Assert.All(items, item => Assert.Null(item.ErrorClass));
        Assert.All(items, item => Assert.Equal(FixedTime.Now, item.HarvestedAt));
        Assert.Equal(SampleFiles.Bytes(SampleFiles.SportMProductIH7234), items[0].Body);
    }

    [Fact]
    public async Task UsesTheTargetUrlWithoutItsContextParameters()
    {
        var given = "http://intersport.mk/ProductCatalog/ProductDetails?Segment=28&ProductID=JQ6920&Grupa=01#sizes";
        var fetched = "http://intersport.mk/ProductCatalog/ProductDetails?ProductID=JQ6920";
        var fetcher = new SampleFetcher();
        fetcher.Serve(fetched, SampleFiles.IntersportProductJQ6920);
        var target = new HarvestTarget { ProviderId = "JQ6920", Url = given };
        var request = Shops.Request(Shops.Intersport) with { Targets = [target] };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        var item = Assert.Single(items);
        Assert.Equal(fetched, item.Url);
        Assert.False(item.Failed);
        Assert.Equal([fetched], fetcher.Requested);
    }

    [Fact]
    public async Task ShoeContextOnANonShoeTargetIsNeverFetched()
    {
        var given = SportMProduct + "KQ6776&Segment=03&Grupa=01";
        var fetcher = new SampleFetcher();
        fetcher.Serve(given, SampleFiles.SportMProductSocksShoeContext);
        fetcher.Serve(SportMProduct + "KQ6776", SampleFiles.SportMProductSocks);
        var target = new HarvestTarget { ProviderId = "KQ6776", Url = given };
        var request = Shops.Request(Shops.SportM) with { Targets = [target] };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        var item = Assert.Single(items);
        Assert.Equal(SportMProduct + "KQ6776", item.Url);
        Assert.Equal(SampleFiles.Bytes(SampleFiles.SportMProductSocks), item.Body);
        Assert.Equal([SportMProduct + "KQ6776"], fetcher.Requested);
    }

    [Fact]
    public async Task AnotherProductOnThePageIsAMismatch()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMProduct + "GW4604", SampleFiles.SportMProductIH7234);
        var request = Shops.Request(Shops.SportM) with { Targets = [Target("GW4604")] };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        var item = Assert.Single(items);
        Assert.Equal("product-mismatch", item.ErrorClass);
        Assert.Equal("requested GW4604, the page shows IH7234", item.ErrorText);
        Assert.Equal("GW4604", item.ProviderId);
        Assert.Empty(item.Body);
    }

    [Fact]
    public async Task PageWithoutProductHeadingIsMissing()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMProduct + "ZZ0000000", SampleFiles.SportMProductMissing);
        var request = Shops.Request(Shops.SportM) with { Targets = [Target("ZZ0000000")] };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        Assert.Equal("product-missing", Assert.Single(items).ErrorClass);
    }

    [Fact]
    public async Task FailuresArePerTarget()
    {
        var fetcher = new SampleFetcher();
        fetcher.Throw(SportMProduct + "IH7234", new TimeoutException("too slow"));
        fetcher.Serve(SportMProduct + "IB2750-001", SampleFiles.SportMProductIB2750);
        var targets = new List<HarvestTarget> { Target("NOPE1"), Target("IH7234"), Target("IB2750-001") };
        var request = Shops.Request(Shops.SportM) with { Targets = targets };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        Assert.Equal(["http-404", "TimeoutException", null], items.Select(item => item.ErrorClass));
        Assert.Equal("too slow", items[1].ErrorText);
    }

    [Fact]
    public async Task LimitCapsTheNumberOfTargets()
    {
        var fetcher = new SampleFetcher();
        fetcher.Serve(SportMProduct + "IH7234", SampleFiles.SportMProductIH7234);
        var targets = new List<HarvestTarget> { Target("IH7234"), Target("IB2750-001") };
        var request = Shops.Request(Shops.SportM) with { Targets = targets, Limit = 1 };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        Assert.Equal("IH7234", Assert.Single(items).ProviderId);
        Assert.Single(fetcher.Requested);
    }

    [Fact]
    public async Task TargetWithoutIdIsAFailedItem()
    {
        var fetcher = new SampleFetcher();
        var request = Shops.Request(Shops.SportM) with { Targets = [Target(" ")] };

        var items = await HarvestRuns.CollectAsync(command, request, fetcher);

        Assert.Equal("missing-id", Assert.Single(items).ErrorClass);
        Assert.Empty(fetcher.Requested);
    }

    private static HarvestTarget Target(string providerId) => new() { ProviderId = providerId };
}
