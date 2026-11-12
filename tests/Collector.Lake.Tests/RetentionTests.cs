using Collector.Core.Lake;
using Collector.Core.Platform;
using MongoDB.Driver;

namespace Collector.Lake.Tests;

[Trait("Category", "Integration")]
public class RetentionTests(LakeFixture fixture) : IClassFixture<LakeFixture>
{
    private readonly LakeStore lake = fixture.NewStore();
    private readonly Guid projectId = Guid.NewGuid();

    private static CancellationToken Token => CancellationToken.None;

    [Fact]
    public async Task Full_history_keeps_every_write_as_a_new_version()
    {
        var write = Writes.For(projectId, "4711", "<html>same</html>");

        var results = new List<LakeWriteResult>();
        for (var i = 0; i < 3; i++) results.Add(await lake.WriteAsync(write, LakeRetention.FullHistory, Token));

        Assert.Equal(new[] { 1, 2, 3 }, results.Select(result => result.Item.Version));
        Assert.All(results, result => Assert.True(result.NewVersion));
        Assert.Equal(3, await CountAsync(Writes.KeyOf(write)));
        Assert.Equal(new[] { "000001.gz", "000002.gz", "000003.gz" }, BodyFiles(results[0].Item));
    }

    [Fact]
    public async Task Changes_only_returns_the_latest_version_for_an_unchanged_body()
    {
        var first = Writes.For(projectId, "4711", "<html>a</html>");
        var again = first with { HarvestedAt = first.HarvestedAt.AddHours(1) };

        var written = await lake.WriteAsync(first, LakeRetention.ChangesOnly, Token);
        var unchanged = await lake.WriteAsync(again, LakeRetention.ChangesOnly, Token);

        Assert.True(written.NewVersion);
        Assert.False(unchanged.NewVersion);
        Assert.Equal(first.HarvestedAt, written.Item.CheckedAt);
        Assert.Equal(written.Item with { CheckedAt = again.HarvestedAt }, unchanged.Item);
        Assert.Equal(1, await CountAsync(Writes.KeyOf(first)));
        Assert.Equal(new[] { "000001.gz" }, BodyFiles(written.Item));
    }

    [Fact]
    public async Task Changes_only_writes_a_new_version_whenever_the_body_differs_from_the_latest()
    {
        var a = Writes.For(projectId, "4711", "<html>a</html>");
        var b = Writes.For(projectId, "4711", "<html>b</html>");

        var first = await lake.WriteAsync(a, LakeRetention.ChangesOnly, Token);
        var second = await lake.WriteAsync(b, LakeRetention.ChangesOnly, Token);
        var third = await lake.WriteAsync(a, LakeRetention.ChangesOnly, Token);
        var fourth = await lake.WriteAsync(a, LakeRetention.ChangesOnly, Token);

        var results = new[] { first, second, third, fourth };
        Assert.Equal(new[] { 1, 2, 3, 3 }, results.Select(result => result.Item.Version));
        Assert.Equal(new[] { true, true, true, false }, results.Select(result => result.NewVersion));
        Assert.Equal(first.Item.Hash, third.Item.Hash);
        Assert.NotEqual(first.Item.Hash, second.Item.Hash);
    }

    [Fact]
    public async Task Latest_only_keeps_a_single_version_on_disk_and_in_the_index()
    {
        var one = Writes.For(projectId, "4711", "<html>one</html>");
        var two = Writes.For(projectId, "4711", "<html>two</html>");
        var key = Writes.KeyOf(one);

        var first = await lake.WriteAsync(one, LakeRetention.LatestOnly, Token);
        var second = await lake.WriteAsync(two, LakeRetention.LatestOnly, Token);
        var third = await lake.WriteAsync(two, LakeRetention.LatestOnly, Token);

        Assert.Equal(new[] { 1, 2, 3 }, new[] { first, second, third }.Select(r => r.Item.Version));
        Assert.All(new[] { first, second, third }, result => Assert.True(result.NewVersion));
        Assert.Equal(1, await CountAsync(key));
        Assert.Equal(new[] { "000003.gz" }, BodyFiles(third.Item));
        Assert.Null(await lake.ReadAsync(projectId, key, 1, Token));
        Assert.Null(await lake.ReadAsync(projectId, key, 2, Token));
        Assert.Equal("<html>two</html>", (await lake.ReadLatestAsync(projectId, key, Token))?.Text);
        await Assert.ThrowsAsync<FileNotFoundException>(() => lake.ReadAsync(first.Item, Token));
    }

    [Fact]
    public async Task Retention_only_touches_the_key_that_is_written()
    {
        var kept = Writes.For(projectId, "4711", "<html>kept</html>");
        var replaced = Writes.For(projectId, "4712", "<html>replaced</html>");

        await lake.WriteAsync(kept, LakeRetention.FullHistory, Token);
        await lake.WriteAsync(kept, LakeRetention.FullHistory, Token);
        await lake.WriteAsync(replaced, LakeRetention.LatestOnly, Token);
        await lake.WriteAsync(replaced, LakeRetention.LatestOnly, Token);

        Assert.Equal(2, await CountAsync(Writes.KeyOf(kept)));
        Assert.Equal(1, await CountAsync(Writes.KeyOf(replaced)));
    }

    private async Task<long> CountAsync(string key)
    {
        var filter = Builders<LakeItem>.Filter.Where(item => item.ProjectId == projectId && item.Key == key);
        return await fixture.Database.LakeItems.CountDocumentsAsync(filter, cancellationToken: Token);
    }

    private string[] BodyFiles(LakeItem item)
    {
        var folder = Path.GetDirectoryName(lake.PathOf(item))!;
        return Directory.GetFiles(folder).Select(Path.GetFileName).OfType<string>().Order().ToArray();
    }
}
