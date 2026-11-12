using System.Net;
using Collector.Core.Platform;
using Collector.Harvesting.Tests.Fakes;

namespace Collector.Harvesting.Tests;

public class RobotsFetchingTests
{
    private const string Robots = "https://shop.test/robots.txt";
    private const string RobotsText = "User-agent: *\nDisallow: /private\n";

    private static readonly Uri First = new("https://shop.test/a");
    private static readonly Uri Second = new("https://shop.test/b");
    private static readonly Uri Private = new("https://shop.test/private/orders");

    private readonly Clock clock = new();
    private readonly FakeHandler handler;

    public RobotsFetchingTests()
    {
        handler = new FakeHandler(clock.Time);
        handler.On(First.ToString(), Respond.Ok("a"));
        handler.On(Second.ToString(), Respond.Ok("b"));
        handler.On(Private.ToString(), Respond.Ok("secret"));
    }

    [Fact]
    public async Task Robots_are_read_once_per_host_and_paced_like_any_request()
    {
        handler.On(Robots, Respond.Status(HttpStatusCode.OK, RobotsText, "text/plain"));
        using var fetcher = Fetcher();

        await clock.Drive(() => GetAll(fetcher, First, Second));

        Assert.Equal([Robots, First.ToString(), Second.ToString()], handler.Urls);
        Assert.Equal(Times.Seconds(0, 1, 2), handler.Times);
    }

    [Fact]
    public async Task A_disallowed_url_throws_and_is_never_requested()
    {
        handler.On(Robots, Respond.Status(HttpStatusCode.OK, RobotsText, "text/plain"));
        using var fetcher = Fetcher();

        var error = await Assert.ThrowsAsync<RobotsDisallowedException>(() => Get(fetcher, Private));

        Assert.Equal(Private, error.Url);
        Assert.Equal([Robots], handler.Urls);
    }

    [Fact]
    public async Task A_missing_robots_allows_everything()
    {
        handler.On(Robots, Respond.Status(HttpStatusCode.NotFound));
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Private);

        Assert.Equal("secret", response.Text);
        Assert.Equal([Robots, Private.ToString()], handler.Urls);
    }

    [Fact]
    public async Task An_unreachable_robots_blocks_the_host_and_is_asked_again_only_after_the_hold()
    {
        var unavailable = Respond.Status(HttpStatusCode.ServiceUnavailable);
        handler.On(Robots, unavailable, unavailable, unavailable, Respond.Status(HttpStatusCode.OK, "", "text/plain"));
        using var fetcher = Fetcher();

        await Assert.ThrowsAsync<RobotsDisallowedException>(() => Get(fetcher, First));
        var blocked = await Assert.ThrowsAsync<RobotsDisallowedException>(() => Get(fetcher, Second));
        await Assert.ThrowsAsync<RobotsDisallowedException>(() => Get(fetcher, Private));
        clock.Time.Advance(TimeSpan.FromMinutes(5));
        var response = await Get(fetcher, First);

        Assert.Equal(Second, blocked.Url);
        Assert.Contains("answered 503", blocked.Message);
        Assert.Equal("a", response.Text);
        Assert.Equal([Robots, Robots, Robots, Robots, First.ToString()], handler.Urls);
        Assert.Equal(Times.Seconds(0, 1, 3, 303, 304), handler.Times);
    }

    [Fact]
    public async Task A_network_error_on_robots_is_a_disallow_too()
    {
        handler.On(Robots, Respond.Fails());
        using var fetcher = Fetcher();

        var error = await Assert.ThrowsAsync<RobotsDisallowedException>(() => Get(fetcher, First));
        await Assert.ThrowsAsync<RobotsDisallowedException>(() => Get(fetcher, Second));

        Assert.IsType<HttpRequestException>(error.InnerException);
        Assert.Equal(First, error.Url);
        Assert.Equal([Robots, Robots, Robots], handler.Urls);
    }

    [Fact]
    public async Task Robots_are_read_when_any_source_of_the_host_respects_them()
    {
        handler.On(Robots, Respond.Status(HttpStatusCode.OK, RobotsText, "text/plain"));
        using var fetcher = Fetcher();
        fetcher.Register(new Source { Key = "careless", BaseUrl = "https://shop.test", RespectRobots = false });
        fetcher.Register(new Source { Key = "careful", BaseUrl = "https://shop.test", RespectRobots = true });

        await Assert.ThrowsAsync<RobotsDisallowedException>(() => Get(fetcher, Private));

        Assert.Equal([Robots], handler.Urls);
    }

    [Fact]
    public async Task Robots_are_skipped_for_a_source_that_does_not_respect_them()
    {
        handler.On(Robots, Respond.Status(HttpStatusCode.OK, "User-agent: *\nDisallow: /\n", "text/plain"));
        using var fetcher = Fetcher();
        fetcher.Register(new Source { Key = "shop_test", BaseUrl = "https://shop.test", RespectRobots = false });

        var response = await Get(fetcher, Private);

        Assert.Equal("secret", response.Text);
        Assert.Equal([Private.ToString()], handler.Urls);
    }

    [Fact]
    public async Task The_saved_sport_vision_robots_blocks_search_and_allows_products()
    {
        var robots = "https://www.sportvision.mk/robots.txt";
        var product = new Uri("https://www.sportvision.mk/patike/nike-air-max-90");
        var search = new Uri("https://www.sportvision.mk/patike?search=nike");
        handler.On(robots, Respond.Status(HttpStatusCode.OK, SampleFile.Read("sportvision_mk.robots.txt")));
        handler.On(product.ToString(), Respond.Ok("air max"));
        using var fetcher = Fetcher();

        var response = await Get(fetcher, product);
        await Assert.ThrowsAsync<RobotsDisallowedException>(() => Get(fetcher, search));

        Assert.Equal("air max", response.Text);
        Assert.Equal([robots, product.ToString()], handler.Urls);
    }

    private PoliteFetcher Fetcher()
    {
        return new PoliteFetcher(handler, new FetcherOptions(), clock.Time);
    }

    private Task<FetchResponse> Get(PoliteFetcher fetcher, Uri url)
    {
        return clock.Drive(() => fetcher.GetAsync(url, CancellationToken.None));
    }

    private static async Task<int> GetAll(PoliteFetcher fetcher, params Uri[] urls)
    {
        foreach (var url in urls) await fetcher.GetAsync(url, CancellationToken.None);
        return urls.Length;
    }
}
