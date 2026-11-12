using System.Text;
using Collector.Core.Ids;
using Collector.Core.Lake;
using Collector.Core.Platform;
using MongoDB.Driver;

namespace Collector.Lake.Tests;

[Trait("Category", "Integration")]
public class ConcurrencyTests(LakeFixture fixture) : IClassFixture<LakeFixture>
{
    private const int Writers = 16;

    private static readonly TimeSpan ShortWait = TimeSpan.FromMilliseconds(200);

    private readonly Guid projectId = Guid.NewGuid();

    private static CancellationToken Token => CancellationToken.None;

    [Fact]
    public async Task Concurrent_writers_on_one_key_get_distinct_versions()
    {
        var stores = new[] { fixture.NewStore(), fixture.NewStore() };
        var writes = Enumerable.Range(0, Writers).Select(i => Writes.For(projectId, "4711", $"body {i}")).ToList();

        var results = await ConcurrentWrites.RunAsync(stores, writes, LakeRetention.FullHistory);

        Assert.Equal(Enumerable.Range(1, Writers), results.Select(result => result.Item.Version).Order());
        Assert.All(results, result => Assert.True(result.NewVersion));
        foreach (var (write, result) in writes.Zip(results))
        {
            var read = await stores[0].ReadAsync(result.Item, Token);
            Assert.Equal(write.Body, read.Body);
        }

        var folder = LakeFolder.Of(stores[0], writes[0]);
        Assert.Equal(Writers, LakeFolder.Bodies(folder).Length);
        Assert.Empty(LakeFolder.Temps(folder));
    }

    [Fact]
    public async Task Writers_on_separate_roots_share_one_version_sequence_through_the_index()
    {
        var stores = new[] { fixture.NewStore("north"), fixture.NewStore("south") };
        var writes = Enumerable.Range(0, Writers).Select(i => Writes.For(projectId, "4711", $"body {i}")).ToList();

        var results = await ConcurrentWrites.RunAsync(stores, writes, LakeRetention.FullHistory);

        Assert.Equal(Enumerable.Range(1, Writers), results.Select(result => result.Item.Version).Order());
        for (var i = 0; i < Writers; i++)
        {
            var read = await stores[i % stores.Length].ReadAsync(results[i].Item, Token);
            Assert.Equal(writes[i].Body, read.Body);
        }

        var bodies = stores.Sum(store => LakeFolder.Bodies(LakeFolder.Of(store, writes[0])).Length);
        Assert.Equal(Writers, bodies);
    }

    [Fact]
    public async Task Concurrent_latest_only_writers_leave_exactly_one_version()
    {
        var stores = new[] { fixture.NewStore(), fixture.NewStore() };
        var writes = Enumerable.Range(0, Writers).Select(i => Writes.For(projectId, "4712", $"body {i}")).ToList();
        var key = Writes.KeyOf(writes[0]);

        var results = await ConcurrentWrites.RunAsync(stores, writes, LakeRetention.LatestOnly);

        var filter = Builders<LakeItem>.Filter.Where(item => item.ProjectId == projectId && item.Key == key);
        var remaining = await fixture.Database.LakeItems.Find(filter).ToListAsync(Token);
        var survivor = Assert.Single(remaining);
        Assert.Equal(results.Max(result => result.Item.Version), survivor.Version);

        var folder = Path.GetDirectoryName(stores[0].PathOf(survivor))!;
        var files = Directory.GetFiles(folder).Select(Path.GetFileName).ToList();
        Assert.Equal(LakePath.FileName(survivor.Version), Assert.Single(files));

        var latest = await stores[1].ReadLatestAsync(projectId, key, Token);
        Assert.NotNull(latest);
        Assert.Contains(latest.Text, writes.Select(write => Encoding.UTF8.GetString(write.Body)));
    }

    [Theory]
    [InlineData(LakeRetention.FullHistory)]
    [InlineData(LakeRetention.ChangesOnly)]
    [InlineData(LakeRetention.LatestOnly)]
    public async Task A_body_left_by_a_crashed_writer_is_never_overwritten(LakeRetention retention)
    {
        var lake = fixture.NewStore("crashed", ShortWait);
        var write = Writes.For(projectId, $"crashed-{retention}", "<html>new</html>");
        var orphan = BodyPath(lake, write, 1);
        Directory.CreateDirectory(Path.GetDirectoryName(orphan)!);
        await File.WriteAllTextAsync(orphan, "orphan", Token);

        var result = await lake.WriteAsync(write, retention, Token);

        Assert.Equal(2, result.Item.Version);
        Assert.True(result.NewVersion);
        Assert.Equal("orphan", await File.ReadAllTextAsync(orphan, Token));
        Assert.Equal("<html>new</html>", (await lake.ReadLatestAsync(projectId, Writes.KeyOf(write), Token))?.Text);
    }

    [Fact]
    public async Task A_crashed_body_with_the_same_hash_never_blocks_a_changes_only_write()
    {
        var lake = fixture.NewStore("crashed", ShortWait);
        var write = Writes.For(projectId, "4714", "<html>same</html>");
        var orphan = BodyPath(lake, write, 1);
        Directory.CreateDirectory(Path.GetDirectoryName(orphan)!);
        var orphanBytes = Gzip.Compress(write.Body);
        await File.WriteAllBytesAsync(orphan, orphanBytes, Token);

        var result = await lake.WriteAsync(write, LakeRetention.ChangesOnly, Token);

        Assert.Equal(2, result.Item.Version);
        Assert.True(result.NewVersion);
        Assert.Equal(orphanBytes, await File.ReadAllBytesAsync(orphan, Token));
        Assert.Equal(write.Body, (await lake.ReadAsync(result.Item, Token)).Body);
    }

    [Theory]
    [InlineData("<html>in flight</html>", 1, false)]
    [InlineData("<html>changed</html>", 2, true)]
    public async Task A_changes_only_writer_settles_against_a_body_whose_index_document_is_still_coming(
        string text, int version, bool newVersion)
    {
        var lake = fixture.NewStore();
        var inFlightWrite = Writes.For(projectId, $"in-flight-{version}", "<html>in flight</html>");
        var inFlight = Placed(inFlightWrite, 1);
        var path = lake.PathOf(inFlight);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, Gzip.Compress(inFlightWrite.Body), Token);

        var changed = inFlightWrite with { Body = Encoding.UTF8.GetBytes(text) };
        var writing = lake.WriteAsync(changed, LakeRetention.ChangesOnly, Token);
        await Task.Delay(TimeSpan.FromMilliseconds(300), Token);
        await fixture.Database.LakeItems.InsertOneAsync(inFlight, cancellationToken: Token);
        var result = await writing;

        Assert.Equal(newVersion, result.NewVersion);
        Assert.Equal(version, result.Item.Version);
        Assert.Equal(text, (await lake.ReadLatestAsync(projectId, inFlight.Key, Token))?.Text);
        Assert.Equal(version, LakeFolder.Bodies(LakeFolder.Of(lake, inFlightWrite)).Length);
        Assert.Empty(LakeFolder.Temps(LakeFolder.Of(lake, inFlightWrite)));
    }

    private LakeItem Placed(LakeWrite write, int version)
    {
        var lakeKey = new LakeKey(projectId, write.Source, write.Kind, write.ProviderId);
        var key = lakeKey.ToString();
        return new LakeItem
        {
            Id = DeterministicId.Create(projectId, $"lake:{key}:{version}"),
            ProjectId = projectId,
            Key = key,
            Source = write.Source,
            Kind = write.Kind,
            ProviderId = write.ProviderId,
            Version = version,
            Url = write.Url,
            ContentType = write.ContentType,
            HarvestedAt = write.HarvestedAt,
            Hash = Gzip.Hash(write.Body),
            Size = write.Body.LongLength,
            Location = LakePath.Location(lakeKey, version),
        };
    }

    private static string BodyPath(LakeStore lake, LakeWrite write, int version)
    {
        return Path.Combine(LakeFolder.Of(lake, write), LakePath.FileName(version));
    }
}
