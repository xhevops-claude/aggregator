using Collector.Core.Lake;
using Collector.Core.Platform;
using MongoDB.Driver;

namespace Collector.Lake.Tests;

[Trait("Category", "Integration")]
public class ChangesOnlyConcurrencyTests(LakeFixture fixture) : IClassFixture<LakeFixture>
{
    private const int Writers = 16;
    private const int Rounds = 20;

    private static readonly string[] Bodies = ["<html>a</html>", "<html>b</html>", "<html>c</html>"];

    private readonly Guid projectId = Guid.NewGuid();

    [Theory]
    [InlineData("main", "main")]
    [InlineData("north", "south")]
    public async Task Writers_of_one_body_leave_exactly_one_version(string firstLake, string secondLake)
    {
        var stores = new[] { fixture.NewStore(firstLake), fixture.NewStore(secondLake) };
        for (var round = 0; round < Rounds; round++)
        {
            var write = Writes.For(projectId, $"same-{firstLake}-{secondLake}-{round}", "<html>same</html>");
            var writes = Enumerable.Repeat(write, Writers).ToList();

            var results = await ConcurrentWrites.RunAsync(stores, writes, LakeRetention.ChangesOnly);

            var stored = Assert.Single(await StoredAsync(write));
            Assert.Single(results, result => result.NewVersion);
            Assert.All(results, result => Assert.Equal(stored, result.Item));
            var folders = stores.Select(store => LakeFolder.Of(store, write)).Distinct().ToList();
            Assert.Equal(1, folders.Sum(folder => LakeFolder.Bodies(folder).Length));
            Assert.All(folders, folder => Assert.Empty(LakeFolder.Temps(folder)));
        }
    }

    [Theory]
    [InlineData("main", "main")]
    [InlineData("east", "west")]
    public async Task Writers_of_one_body_at_different_times_leave_the_latest_time_as_checked_at(
        string firstLake, string secondLake)
    {
        var stores = new[] { fixture.NewStore(firstLake), fixture.NewStore(secondLake) };
        for (var round = 0; round < Rounds; round++)
        {
            var write = Writes.For(projectId, $"timed-{round}", "<html>same</html>");
            var writes = Enumerable.Range(0, Writers).Select(i => write with { HarvestedAt = Minutes(i) }).ToList();
            var latestHarvest = writes.Max(item => item.HarvestedAt);

            var results = await ConcurrentWrites.RunAsync(stores, writes, LakeRetention.ChangesOnly);

            var stored = Assert.Single(await StoredAsync(write));
            Assert.Single(results, result => result.NewVersion);
            Assert.Equal(latestHarvest, stored.CheckedAt);
            Assert.Contains(stored.HarvestedAt, writes.Select(item => item.HarvestedAt));
            Assert.All(results, result => Assert.Equal(stored with { CheckedAt = default }, Unchecked(result)));
        }
    }

    [Fact]
    public async Task Writers_of_mixed_bodies_never_store_one_body_twice_in_a_row()
    {
        var stores = new[] { fixture.NewStore(), fixture.NewStore() };
        for (var round = 0; round < Rounds; round++)
        {
            var providerId = $"mixed-{round}";
            var writes = Enumerable.Range(0, Writers).Select(i => Write(providerId, i)).ToList();

            var results = await ConcurrentWrites.RunAsync(stores, writes, LakeRetention.ChangesOnly);

            var stored = await StoredAsync(writes[0]);
            Assert.Equal(Enumerable.Range(1, stored.Count), stored.Select(item => item.Version));
            Assert.All(stored.Zip(stored.Skip(1)), pair => Assert.NotEqual(pair.First.Hash, pair.Second.Hash));
            var newVersions = results.Where(result => result.NewVersion).Select(result => result.Item.Version);
            Assert.Equal(stored.Select(item => item.Version), newVersions.Order());
            foreach (var (write, result) in writes.Zip(results))
            {
                Assert.Equal(Gzip.Hash(write.Body), result.Item.Hash);
                Assert.Contains(result.Item, stored);
            }
        }
    }

    private static DateTime Minutes(int writer) => Writes.Harvested.AddMinutes(writer);

    private static LakeItem Unchecked(LakeWriteResult result) => result.Item with { CheckedAt = default };

    private LakeWrite Write(string providerId, int writer)
    {
        return Writes.For(projectId, providerId, Bodies[writer % Bodies.Length]);
    }

    private async Task<List<LakeItem>> StoredAsync(LakeWrite write)
    {
        var key = Writes.KeyOf(write);
        var filter = Builders<LakeItem>.Filter.Where(item => item.ProjectId == projectId && item.Key == key);
        var sort = Builders<LakeItem>.Sort.Ascending(item => item.Version);
        return await fixture.Database.LakeItems.Find(filter).Sort(sort).ToListAsync(CancellationToken.None);
    }
}
