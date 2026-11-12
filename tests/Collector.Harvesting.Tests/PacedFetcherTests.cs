using Collector.Core.Platform;
using Collector.Harvesting.Tests.Fakes;

namespace Collector.Harvesting.Tests;

public class PacedFetcherTests
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private readonly Clock clock = new();
    private readonly FakeHandler handler;
    private readonly HostBudget budget;

    public PacedFetcherTests()
    {
        handler = new FakeHandler(clock.Time);
        budget = new HostBudget(clock.Time);
    }

    [Fact]
    public async Task Burst_keeps_only_the_source_floor()
    {
        using var polite = Polite(OneSecond);
        var paced = Paced(polite, new Pacing { Mode = PacingMode.Burst });

        await clock.Drive(() => FetchAll(paced, 3));

        Assert.Equal(Times.Seconds(0, 1, 2), handler.Times);
    }

    [Fact]
    public async Task Burst_without_a_floor_sends_back_to_back()
    {
        using var polite = Polite(TimeSpan.Zero);
        var paced = Paced(polite, new Pacing { Mode = PacingMode.Burst });

        await clock.Drive(() => FetchAll(paced, 3));

        Assert.Equal(Times.Seconds(0, 0, 0), handler.Times);
    }

    [Fact]
    public async Task Interval_spaces_requests_by_the_interval()
    {
        using var polite = Polite(OneSecond);
        var paced = Paced(polite, new Pacing { Mode = PacingMode.Interval, Interval = TimeSpan.FromSeconds(5) });

        await clock.Drive(() => FetchAll(paced, 3));

        Assert.Equal(Times.Seconds(0, 5, 10), handler.Times);
    }

    [Fact]
    public async Task A_strategy_never_goes_faster_than_the_floor()
    {
        using var polite = Polite(TimeSpan.FromSeconds(2));
        var pacing = new Pacing { Mode = PacingMode.Interval, Interval = TimeSpan.FromMilliseconds(500) };
        var paced = Paced(polite, pacing);

        await clock.Drive(() => FetchAll(paced, 3));

        Assert.Equal(Times.Seconds(0, 2, 4), handler.Times);
    }

    [Fact]
    public async Task The_floor_counts_from_the_end_of_a_slow_request()
    {
        var slow = Respond.Slow(clock.Time, TimeSpan.FromMilliseconds(500));
        for (var i = 0; i < 3; i++) handler.On(Page(i).ToString(), slow);
        using var polite = Polite(OneSecond);
        var paced = Paced(polite, new Pacing { Mode = PacingMode.Interval, Interval = OneSecond });

        await clock.Drive(() => FetchAll(paced, 3));

        Assert.Equal(Times.Seconds(0, 1.5, 3), handler.Times);
    }

    [Fact]
    public async Task Jitter_waits_between_the_interval_and_the_interval_plus_jitter()
    {
        var interval = TimeSpan.FromSeconds(2);
        var jitter = TimeSpan.FromSeconds(3);
        var pacing = new Pacing { Mode = PacingMode.Jitter, Interval = interval, Jitter = jitter };
        using var polite = Polite(OneSecond);
        var paced = new PacedFetcher(polite, pacing, null, budget, clock.Time, new Random(7));

        await clock.Drive(() => FetchAll(paced, 8));

        var starts = handler.Times;
        var gaps = starts.Zip(starts.Skip(1), (previous, next) => next - previous).ToList();
        Assert.All(gaps, gap => Assert.InRange(gap, interval, interval + jitter));
        Assert.True(gaps.Distinct().Count() > 1, "jitter should not give one fixed gap");
    }

    [Fact]
    public async Task Spread_divides_the_window_by_the_expected_requests()
    {
        var pacing = new Pacing { Mode = PacingMode.Spread, Window = TimeSpan.FromHours(1) };
        using var polite = Polite(OneSecond);
        var paced = new PacedFetcher(polite, pacing, 4, budget, clock.Time);

        await clock.Drive(() => FetchAll(paced, 4));

        Assert.Equal(Times.Seconds(0, 900, 1800, 2700), handler.Times);
    }

    [Fact]
    public async Task Spread_without_an_expected_count_falls_back_to_the_floor()
    {
        using var polite = Polite(OneSecond);
        var paced = Paced(polite, new Pacing { Mode = PacingMode.Spread, Window = TimeSpan.FromHours(1) });

        await clock.Drive(() => FetchAll(paced, 3));

        Assert.Equal(Times.Seconds(0, 1, 2), handler.Times);
    }

    [Fact]
    public async Task Budget_allows_at_most_max_per_rolling_hour()
    {
        using var polite = Polite(OneSecond);
        var paced = Paced(polite, new Pacing { Mode = PacingMode.Budget, MaxPerHour = 3 });

        await clock.Drive(() => FetchAll(paced, 5));

        Assert.Equal(Times.Seconds(0, 1, 2, 3600, 3601), handler.Times);
    }

    [Fact]
    public async Task Commands_on_one_host_share_its_budget()
    {
        using var polite = Polite(OneSecond);
        var pacing = new Pacing { Mode = PacingMode.Budget, MaxPerHour = 2 };
        var first = Paced(polite, pacing);
        var second = Paced(polite, pacing);

        await clock.Drive(() => FetchAll(first, 2));
        await clock.Drive(() => FetchAll(second, 2));

        Assert.Equal(Times.Seconds(0, 1, 3600, 3601), handler.Times);
    }

    [Fact]
    public async Task Concurrent_commands_on_one_host_share_its_budget()
    {
        using var polite = Polite(OneSecond);
        var pacing = new Pacing { Mode = PacingMode.Budget, MaxPerHour = 2 };
        var first = Paced(polite, pacing);
        var second = Paced(polite, pacing);

        await clock.Drive(() => FetchTogether(FetchAll(first, 2), FetchAll(second, 2)));

        Assert.Equal(Times.Seconds(0, 1, 3600, 3601), handler.Times);
    }

    [Fact]
    public async Task Budgets_of_different_hosts_do_not_mix()
    {
        using var polite = Polite(OneSecond);
        var pacing = new Pacing { Mode = PacingMode.Budget, MaxPerHour = 2 };
        var shop = Paced(polite, pacing);
        var other = Paced(polite, pacing);

        await clock.Drive(() => FetchAll(shop, 2));
        await clock.Drive(() => FetchAll(other, 2, "https://other.test"));

        Assert.Equal(Times.Seconds(0, 1, 1, 2), handler.Times);
    }

    private PoliteFetcher Polite(TimeSpan floor)
    {
        var host = new HostSettings { MinRequestInterval = floor, RespectRobots = false };
        return new PoliteFetcher(handler, new FetcherOptions { DefaultHost = host }, clock.Time);
    }

    private PacedFetcher Paced(IFetcher inner, Pacing pacing)
    {
        return new PacedFetcher(inner, pacing, null, budget, clock.Time);
    }

    private static Uri Page(int number, string site = "https://shop.test")
    {
        return new Uri($"{site}/p/{number}");
    }

    private static async Task<int> FetchAll(IFetcher fetcher, int count, string site = "https://shop.test")
    {
        for (var i = 0; i < count; i++) await fetcher.GetAsync(Page(i, site), CancellationToken.None);
        return count;
    }

    private static async Task<int> FetchTogether(params Task<int>[] fetches)
    {
        var counts = await Task.WhenAll(fetches);
        return counts.Sum();
    }
}
