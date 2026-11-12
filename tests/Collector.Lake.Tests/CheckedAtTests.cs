using Collector.Core.Lake;
using Collector.Core.Platform;
using MongoDB.Driver;

namespace Collector.Lake.Tests;

[Trait("Category", "Integration")]
public class CheckedAtTests(LakeFixture fixture) : IClassFixture<LakeFixture>
{
    private readonly LakeStore lake = fixture.NewStore();
    private readonly Guid projectId = Guid.NewGuid();

    private static CancellationToken Token => CancellationToken.None;

    [Theory]
    [InlineData(LakeRetention.FullHistory)]
    [InlineData(LakeRetention.ChangesOnly)]
    [InlineData(LakeRetention.LatestOnly)]
    public async Task A_new_version_is_checked_when_it_is_harvested(LakeRetention retention)
    {
        var write = Writes.For(projectId, $"new-{retention}", "<html>a</html>");

        var result = await lake.WriteAsync(write, retention, Token);

        Assert.True(result.NewVersion);
        Assert.Equal(write.HarvestedAt, result.Item.CheckedAt);
        Assert.Equal(write.HarvestedAt, (await StoredAsync(write)).CheckedAt);
    }

    [Fact]
    public async Task An_unchanged_re_harvest_moves_checked_at_and_keeps_the_version()
    {
        var first = Writes.For(projectId, "unchanged", "<html>a</html>");
        var again = first with { HarvestedAt = first.HarvestedAt.AddHours(1) };

        var written = await lake.WriteAsync(first, LakeRetention.ChangesOnly, Token);
        await lake.WriteAsync(again, LakeRetention.ChangesOnly, Token);

        var stored = await StoredAsync(first);
        Assert.Equal(written.Item.Version, stored.Version);
        Assert.Equal(written.Item.Hash, stored.Hash);
        Assert.Equal(first.HarvestedAt, stored.HarvestedAt);
        Assert.Equal(again.HarvestedAt, stored.CheckedAt);
        var latest = await lake.ReadLatestAsync(projectId, Writes.KeyOf(first), Token);
        Assert.Equal(again.HarvestedAt, latest?.Item.LastCheckedAt());
    }

    [Fact]
    public async Task An_older_re_harvest_never_moves_checked_at_back()
    {
        var first = Writes.For(projectId, "older", "<html>a</html>");
        var newest = first with { HarvestedAt = first.HarvestedAt.AddHours(2) };
        var older = first with { HarvestedAt = first.HarvestedAt.AddHours(1) };

        await lake.WriteAsync(first, LakeRetention.ChangesOnly, Token);
        await lake.WriteAsync(newest, LakeRetention.ChangesOnly, Token);
        var late = await lake.WriteAsync(older, LakeRetention.ChangesOnly, Token);

        Assert.False(late.NewVersion);
        Assert.Equal(newest.HarvestedAt, late.Item.CheckedAt);
        Assert.Equal(newest.HarvestedAt, (await StoredAsync(first)).CheckedAt);
    }

    [Fact]
    public async Task A_row_stored_without_checked_at_falls_back_to_harvested_at_until_its_next_check()
    {
        var first = Writes.For(projectId, "legacy", "<html>a</html>");
        var again = first with { HarvestedAt = first.HarvestedAt.AddHours(3) };
        await lake.WriteAsync(first, LakeRetention.ChangesOnly, Token);
        await RemoveCheckedAtAsync(first);

        var legacy = await StoredAsync(first);
        var touched = await lake.WriteAsync(again, LakeRetention.ChangesOnly, Token);

        Assert.Equal(default, legacy.CheckedAt);
        Assert.Equal(first.HarvestedAt, legacy.LastCheckedAt());
        Assert.Equal(again.HarvestedAt, touched.Item.CheckedAt);
        Assert.Equal(again.HarvestedAt, (await StoredAsync(first)).LastCheckedAt());
    }

    private async Task RemoveCheckedAtAsync(LakeWrite write)
    {
        var update = Builders<LakeItem>.Update.Unset(item => item.CheckedAt);
        await fixture.Database.LakeItems.UpdateOneAsync(KeyFilter(write), update, cancellationToken: Token);
    }

    private async Task<LakeItem> StoredAsync(LakeWrite write)
    {
        return await fixture.Database.LakeItems.Find(KeyFilter(write)).SingleAsync(Token);
    }

    private FilterDefinition<LakeItem> KeyFilter(LakeWrite write)
    {
        var key = Writes.KeyOf(write);
        return Builders<LakeItem>.Filter.Where(item => item.ProjectId == projectId && item.Key == key);
    }
}
