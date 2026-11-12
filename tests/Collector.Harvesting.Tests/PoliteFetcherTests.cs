using System.Net;
using Collector.Core.Platform;
using Collector.Harvesting.Tests.Fakes;

namespace Collector.Harvesting.Tests;

public class PoliteFetcherTests
{
    private static readonly Uri Page = new("https://shop.test/p/1");
    private static readonly Uri Other = new("https://shop.test/p/2");

    private readonly Clock clock = new();
    private readonly FakeHandler handler;

    public PoliteFetcherTests()
    {
        handler = new FakeHandler(clock.Time);
    }

    [Fact]
    public async Task Sends_a_desktop_chrome_user_agent_and_macedonian_accept_language()
    {
        handler.On(Page.ToString(), Respond.Ok());
        using var fetcher = Fetcher();

        await Get(fetcher, Page);

        var seen = Assert.Single(handler.Seen);
        Assert.StartsWith("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", seen.UserAgent);
        Assert.Contains("Chrome/", seen.UserAgent);
        Assert.Equal("mk,en;q=0.8", seen.AcceptLanguage);
    }

    [Fact]
    public async Task Returns_status_body_and_content_type()
    {
        handler.On(Page.ToString(), Respond.Status(HttpStatusCode.OK, "<html>shoe</html>"));
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Page);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("<html>shoe</html>", response.Text);
        Assert.StartsWith("text/html", response.ContentType);
        Assert.Equal(Page, response.Url);
    }

    [Fact]
    public async Task Returns_the_final_url_after_redirects()
    {
        handler.On(Page.ToString(), Respond.Redirected("https://shop.test/p/1-new", "moved here"));
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Page);

        Assert.Equal(new Uri("https://shop.test/p/1-new"), response.Url);
        Assert.Equal("moved here", response.Text);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Other_4xx_come_back_once_without_retry(HttpStatusCode status)
    {
        handler.On(Page.ToString(), Respond.Status(status));
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Page);

        Assert.Equal((int)status, response.StatusCode);
        Assert.False(response.IsSuccess);
        Assert.Single(handler.Seen);
    }

    [Fact]
    public async Task Retries_a_5xx_with_a_doubling_delay()
    {
        var unavailable = Respond.Status(HttpStatusCode.ServiceUnavailable);
        var badGateway = Respond.Status(HttpStatusCode.BadGateway);
        handler.On(Page.ToString(), unavailable, badGateway, Respond.Ok("finally"));
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Page);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("finally", response.Text);
        Assert.Equal(Times.Seconds(0, 1, 3), handler.Times);
    }

    [Fact]
    public async Task Returns_the_last_5xx_after_three_attempts()
    {
        handler.On(Page.ToString(), Respond.Status(HttpStatusCode.InternalServerError));
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Page);

        Assert.Equal(500, response.StatusCode);
        Assert.Equal(Times.Seconds(0, 1, 3), handler.Times);
    }

    [Fact]
    public async Task Recovers_after_a_network_error()
    {
        handler.On(Page.ToString(), Respond.Fails(), Respond.Ok("back"));
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Page);

        Assert.Equal("back", response.Text);
        Assert.Equal(Times.Seconds(0, 1), handler.Times);
    }

    [Fact]
    public async Task Throws_the_network_error_after_three_attempts()
    {
        handler.On(Page.ToString(), Respond.Fails());
        using var fetcher = Fetcher();

        await Assert.ThrowsAsync<HttpRequestException>(() => Get(fetcher, Page));

        Assert.Equal(Times.Seconds(0, 1, 3), handler.Times);
    }

    [Fact]
    public async Task Honours_retry_after_on_429()
    {
        var tooMany = Respond.RetryAfter(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(5));
        handler.On(Page.ToString(), tooMany, Respond.Ok());
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Page);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(Times.Seconds(0, 5), handler.Times);
    }

    [Fact]
    public async Task Honours_a_retry_after_date_on_503()
    {
        var unavailable = Respond.RetryAt(HttpStatusCode.ServiceUnavailable, Clock.Start.AddSeconds(7));
        handler.On(Page.ToString(), unavailable, Respond.Ok());
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Page);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(Times.Seconds(0, 7), handler.Times);
    }

    [Fact]
    public async Task A_retry_after_beyond_the_maximum_is_returned_without_waiting()
    {
        var tooMany = Respond.RetryAfter(HttpStatusCode.TooManyRequests, TimeSpan.FromHours(1));
        handler.On(Page.ToString(), tooMany, Respond.Ok());
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Page);

        Assert.Equal(429, response.StatusCode);
        Assert.Single(handler.Seen);
    }

    [Fact]
    public async Task A_long_retry_after_holds_the_whole_host()
    {
        var tooMany = Respond.RetryAfter(HttpStatusCode.TooManyRequests, TimeSpan.FromMinutes(10));
        handler.On(Page.ToString(), tooMany);
        handler.On(Other.ToString(), Respond.Ok("later"));
        using var fetcher = Fetcher();

        var limited = await Get(fetcher, Page);
        var error = await Assert.ThrowsAsync<HostRateLimitedException>(() => Get(fetcher, Other));
        clock.Time.Advance(TimeSpan.FromMinutes(9));
        var response = await Get(fetcher, Other);

        Assert.Equal(429, limited.StatusCode);
        Assert.Equal(Other, error.Url);
        Assert.Equal(Clock.Start.AddMinutes(10), error.Until);
        Assert.Equal("later", response.Text);
        Assert.Equal([Page.ToString(), Other.ToString()], handler.Urls);
        Assert.Equal(Times.Seconds(0, 600), handler.Times);
    }

    [Fact]
    public async Task A_retry_after_on_the_last_attempt_still_holds_the_host()
    {
        var tooMany = Respond.RetryAfter(HttpStatusCode.TooManyRequests, TimeSpan.FromSeconds(30));
        handler.On(Page.ToString(), tooMany);
        handler.On(Other.ToString(), Respond.Ok());
        using var fetcher = Fetcher();

        await clock.Drive(() => GetAll(fetcher, Page, Other));

        Assert.Equal([Page.ToString(), Page.ToString(), Page.ToString(), Other.ToString()], handler.Urls);
        Assert.Equal(Times.Seconds(0, 30, 60, 90), handler.Times);
    }

    [Fact]
    public async Task Times_out_after_forty_seconds_and_retries()
    {
        handler.On(Page.ToString(), Respond.Hangs(), Respond.Ok("second try"));
        using var fetcher = Fetcher();

        var response = await Get(fetcher, Page);

        Assert.Equal("second try", response.Text);
        Assert.Equal(Times.Seconds(0, 41), handler.Times);
    }

    [Fact]
    public async Task Throws_a_timeout_when_every_attempt_hangs()
    {
        handler.On(Page.ToString(), Respond.Hangs());
        using var fetcher = Fetcher();

        await Assert.ThrowsAsync<TimeoutException>(() => Get(fetcher, Page));

        Assert.Equal(Times.Seconds(0, 41, 83), handler.Times);
    }

    [Fact]
    public async Task Spaces_requests_to_one_host_by_one_second()
    {
        handler.On(Page.ToString(), Respond.Ok());
        handler.On(Other.ToString(), Respond.Ok());
        using var fetcher = Fetcher();

        await clock.Drive(() => GetAll(fetcher, Page, Other, Page));

        Assert.Equal(Times.Seconds(0, 1, 2), handler.Times);
    }

    [Fact]
    public async Task Different_hosts_do_not_wait_for_each_other()
    {
        var first = new Uri("https://a.test/");
        var second = new Uri("https://b.test/");
        handler.On(first.ToString(), Respond.Ok());
        handler.On(second.ToString(), Respond.Ok());
        using var fetcher = Fetcher();

        await clock.Drive(() => GetAll(fetcher, first, second));

        Assert.Equal(Times.Seconds(0, 0), handler.Times);
    }

    [Fact]
    public async Task Uses_the_interval_configured_for_a_host()
    {
        handler.On(Page.ToString(), Respond.Ok());
        var slow = new HostSettings { MinRequestInterval = TimeSpan.FromSeconds(2), RespectRobots = false };
        var options = NoRobots() with { Hosts = new() { ["shop.test"] = slow } };
        using var fetcher = Fetcher(options);

        await clock.Drive(() => GetAll(fetcher, Page, Page));

        Assert.Equal(Times.Seconds(0, 2), handler.Times);
    }

    [Fact]
    public async Task Uses_the_interval_of_a_registered_source()
    {
        handler.On(Page.ToString(), Respond.Ok());
        using var fetcher = Fetcher();
        var source = new Source
        {
            Key = "shop_test",
            BaseUrl = "https://shop.test",
            MinRequestInterval = TimeSpan.FromSeconds(3),
            RespectRobots = false,
        };
        fetcher.Register(source);

        await clock.Drive(() => GetAll(fetcher, Page, Page));

        Assert.Equal(Times.Seconds(0, 3), handler.Times);
    }

    [Fact]
    public async Task Sources_on_one_host_share_the_strictest_floor()
    {
        handler.On(Page.ToString(), Respond.Ok());
        using var fetcher = Fetcher();
        var slow = ShopSource("slow", TimeSpan.FromSeconds(3));
        fetcher.Register(ShopSource("calm", TimeSpan.FromSeconds(1)));
        fetcher.Register(slow);

        await clock.Drive(() => GetAll(fetcher, Page, Page));
        fetcher.Register(slow with { MinRequestInterval = TimeSpan.FromSeconds(1) });
        await clock.Drive(() => GetAll(fetcher, Page, Page));

        Assert.Equal(Times.Seconds(0, 3, 6, 7), handler.Times);
    }

    [Fact]
    public void A_source_without_an_absolute_base_url_cannot_be_registered()
    {
        using var fetcher = Fetcher();

        Assert.Throws<ArgumentException>(() => fetcher.Register(new Source { Key = "nowhere", BaseUrl = "" }));
    }

    [Fact]
    public async Task Sends_one_request_at_a_time_per_host()
    {
        handler.On(Page.ToString(), Respond.Slow(clock.Time, TimeSpan.FromMilliseconds(500)));
        handler.On(Other.ToString(), Respond.Slow(clock.Time, TimeSpan.FromMilliseconds(500)));
        using var fetcher = Fetcher();

        await clock.Drive(() => GetTogether(fetcher, Page, Other));

        Assert.Equal(1, handler.MostInFlight);
        Assert.Equal(Times.Seconds(0, 1.5), handler.Times);
    }

    private static Source ShopSource(string key, TimeSpan floor)
    {
        return new Source
        {
            Key = key,
            BaseUrl = "https://shop.test",
            MinRequestInterval = floor,
            RespectRobots = false,
        };
    }

    private static FetcherOptions NoRobots()
    {
        return new FetcherOptions { DefaultHost = new HostSettings { RespectRobots = false } };
    }

    private PoliteFetcher Fetcher()
    {
        return Fetcher(NoRobots());
    }

    private PoliteFetcher Fetcher(FetcherOptions options)
    {
        return new PoliteFetcher(handler, options, clock.Time);
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

    private static async Task<int> GetTogether(PoliteFetcher fetcher, params Uri[] urls)
    {
        var requests = urls.Select(url => fetcher.GetAsync(url, CancellationToken.None)).ToList();
        await Task.WhenAll(requests);
        return requests.Count;
    }
}
