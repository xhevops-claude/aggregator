using Collector.Core.Lake;
using Collector.Core.Platform;

namespace Collector.Lake.Tests;

[Trait("Category", "Integration")]
public class LatestQueryTests(LakeFixture fixture) : IClassFixture<LakeFixture>
{
    private static readonly DateTime T0 = Writes.Harvested;
    private static readonly DateTime T1 = T0.AddHours(1);
    private static readonly DateTime T2 = T0.AddHours(2);
    private static readonly DateTime T3 = T0.AddHours(3);

    private readonly LakeStore lake = fixture.NewStore();
    private readonly Guid projectId = Guid.NewGuid();

    private static CancellationToken Token => CancellationToken.None;

    [Fact]
    public async Task Returns_the_latest_version_of_every_key_of_a_source_ordered_by_key()
    {
        await SeedAsync();

        var latest = await lake.LatestAsync(Query(), Token);

        var expected = new[]
        {
            ("listing-page", "1", 3),
            ("product", "1", 2),
            ("product", "2", 1),
            ("sitemap", "main", 1),
        };
        Assert.Equal(expected, latest.Select(item => (item.Kind, item.ProviderId, item.Version)));
        Assert.Equal(latest.Select(item => item.Key).Order(StringComparer.Ordinal), latest.Select(item => item.Key));
        Assert.All(latest, item => Assert.Equal("sportm_mk", item.Source));
    }

    [Fact]
    public async Task Kinds_narrow_the_result()
    {
        await SeedAsync();

        var products = await lake.LatestAsync(Query() with { Kinds = ["product"] }, Token);
        var two = await lake.LatestAsync(Query() with { Kinds = ["product", "sitemap"] }, Token);

        Assert.Equal(new[] { ("1", 2), ("2", 1) }, products.Select(item => (item.ProviderId, item.Version)));
        Assert.Equal(3, two.Count);
        Assert.Empty(await lake.LatestAsync(Query() with { Kinds = ["unknown"] }, Token));
    }

    [Fact]
    public async Task Harvested_since_keeps_keys_whose_latest_version_is_recent_enough()
    {
        await SeedAsync();

        var recent = await lake.LatestAsync(Query() with { HarvestedSince = T2 }, Token);

        var expected = new[] { ("listing-page", "1", 3), ("product", "1", 2) };
        Assert.Equal(expected, recent.Select(item => (item.Kind, item.ProviderId, item.Version)));
    }

    [Fact]
    public async Task Harvested_since_is_applied_to_the_latest_version_and_not_to_older_ones()
    {
        await WriteAsync("product", "9", "late harvest, first version", T3);
        await WriteAsync("product", "9", "early harvest, second version", T0);

        var recent = await lake.LatestAsync(Query() with { HarvestedSince = T2 }, Token);
        var all = await lake.LatestAsync(Query(), Token);

        Assert.Empty(recent);
        Assert.Equal(2, Assert.Single(all).Version);
    }

    [Fact]
    public async Task Harvested_since_without_a_kind_is_read_as_utc_like_the_harvest_time()
    {
        var harvested = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Unspecified);
        await WriteAsync("product", "unspecified", "unspecified harvest", harvested);

        var later = await lake.LatestAsync(Query() with { HarvestedSince = harvested.AddMinutes(30) }, Token);
        var earlier = await lake.LatestAsync(Query() with { HarvestedSince = harvested.AddMinutes(-30) }, Token);

        Assert.Empty(later);
        Assert.Equal("unspecified", Assert.Single(earlier).ProviderId);
    }

    [Fact]
    public async Task Harvested_since_in_local_time_is_compared_in_utc()
    {
        var harvested = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
        await WriteAsync("product", "local", "local harvest", harvested);

        var later = harvested.AddMinutes(30).ToLocalTime();
        var earlier = harvested.AddMinutes(-30).ToLocalTime();

        Assert.Empty(await lake.LatestAsync(Query() with { HarvestedSince = later }, Token));
        Assert.Single(await lake.LatestAsync(Query() with { HarvestedSince = earlier }, Token));
    }

    [Fact]
    public async Task Limit_takes_the_first_keys_in_order()
    {
        await SeedAsync();

        var limited = await lake.LatestAsync(Query() with { Limit = 2 }, Token);

        Assert.Equal(new[] { "listing-page", "product" }, limited.Select(item => item.Kind));
        Assert.Equal(new[] { 3, 2 }, limited.Select(item => item.Version));
    }

    [Fact]
    public async Task Other_sources_and_other_projects_stay_out()
    {
        await SeedAsync();

        var other = await lake.LatestAsync(Query() with { Source = "nbshop_mk" }, Token);
        var otherProject = await lake.LatestAsync(Query() with { ProjectId = Guid.NewGuid() }, Token);
        var everySource = await lake.LatestAsync(Query() with { Source = "" }, Token);

        Assert.Equal("1", Assert.Single(other).ProviderId);
        Assert.Empty(otherProject);
        Assert.Equal(5, everySource.Count);
    }

    private LakeQuery Query() => new() { ProjectId = projectId, Source = "sportm_mk" };

    private async Task SeedAsync()
    {
        await WriteAsync("product", "1", "p1 v1", T0);
        await WriteAsync("product", "1", "p1 v2", T2);
        await WriteAsync("product", "2", "p2 v1", T1);
        await WriteAsync("listing-page", "1", "l1 v1", T0);
        await WriteAsync("listing-page", "1", "l1 v2", T1);
        await WriteAsync("listing-page", "1", "l1 v3", T3);
        await WriteAsync("sitemap", "main", "sitemap v1", T0);

        var otherSource = Writes.For(projectId, "1", "nbshop p1") with { Source = "nbshop_mk", HarvestedAt = T3 };
        await lake.WriteAsync(otherSource, LakeRetention.FullHistory, Token);
        var otherProject = Writes.For(Guid.NewGuid(), "1", "other project p1");
        await lake.WriteAsync(otherProject, LakeRetention.FullHistory, Token);
    }

    private async Task WriteAsync(string kind, string providerId, string text, DateTime harvestedAt)
    {
        var write = Writes.For(projectId, providerId, text) with { Kind = kind, HarvestedAt = harvestedAt };
        await lake.WriteAsync(write, LakeRetention.FullHistory, Token);
    }
}
