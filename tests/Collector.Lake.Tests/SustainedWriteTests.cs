using Collector.Core.Lake;
using Collector.Core.Platform;
using MongoDB.Driver;

namespace Collector.Lake.Tests;

[Trait("Category", "Integration")]
public class SustainedWriteTests(LakeFixture fixture) : IClassFixture<LakeFixture>
{
    private const int Stores = 4;
    private const int Writers = 16;
    private const int WritesPerWriter = 15;
    private const int Readers = 4;

    private readonly Guid projectId = Guid.NewGuid();

    [Theory]
    [InlineData(LakeRetention.FullHistory)]
    [InlineData(LakeRetention.ChangesOnly)]
    [InlineData(LakeRetention.LatestOnly)]
    public async Task Writers_hammering_one_key_round_after_round_always_get_a_version(LakeRetention retention)
    {
        var stores = Enumerable.Range(0, Stores).Select(_ => fixture.NewStore()).ToArray();
        var providerId = $"sustained-{retention}";
        var key = Writes.KeyOf(Writes.For(projectId, providerId, ""));
        using var stop = new CancellationTokenSource();

        var readTasks = new List<Task<int>>();
        for (var i = 0; i < Readers; i++) readTasks.Add(StartReader(stores[i % Stores], key, stop.Token));
        var writeTasks = new List<Task<List<LakeWriteResult>>>();
        for (var i = 0; i < Writers; i++) writeTasks.Add(StartWriter(stores[i % Stores], providerId, i, retention));
        var results = (await Task.WhenAll(writeTasks)).SelectMany(rounds => rounds).ToList();
        await stop.CancelAsync();
        var reads = await Task.WhenAll(readTasks);

        Assert.Equal(Writers * WritesPerWriter, results.Count);
        Assert.All(reads, count => Assert.True(count > 0));
        var stored = await StoredAsync(key);
        var folder = Path.GetDirectoryName(stores[0].PathOf(stored[^1]))!;
        Assert.Empty(LakeFolder.Temps(folder));
        if (retention == LakeRetention.LatestOnly)
        {
            var survivor = Assert.Single(stored);
            Assert.Equal(results.Max(result => result.Item.Version), survivor.Version);
            Assert.Single(LakeFolder.Bodies(folder));
            return;
        }

        Assert.All(results, result => Assert.True(result.NewVersion));
        Assert.Equal(Enumerable.Range(1, results.Count), stored.Select(item => item.Version));
        Assert.Equal(results.Count, LakeFolder.Bodies(folder).Length);
    }

    private Task<List<LakeWriteResult>> StartWriter(
        LakeStore store, string providerId, int writer, LakeRetention retention)
    {
        return Task.Run(() => WriteRoundsAsync(store, providerId, writer, retention));
    }

    private Task<int> StartReader(LakeStore store, string key, CancellationToken stop)
    {
        return Task.Run(() => ReadUntilStoppedAsync(store, key, stop));
    }

    private async Task<List<LakeWriteResult>> WriteRoundsAsync(
        LakeStore store, string providerId, int writer, LakeRetention retention)
    {
        var results = new List<LakeWriteResult>();
        for (var round = 0; round < WritesPerWriter; round++)
        {
            var write = Writes.For(projectId, providerId, $"<html>writer {writer} round {round}</html>");
            results.Add(await store.WriteAsync(write, retention, CancellationToken.None));
        }

        return results;
    }

    private async Task<int> ReadUntilStoppedAsync(LakeStore store, string key, CancellationToken stop)
    {
        var reads = 0;
        while (!stop.IsCancellationRequested)
        {
            var read = await store.ReadLatestAsync(projectId, key, CancellationToken.None);
            if (read is not null) reads++;
        }

        return reads;
    }

    private async Task<List<LakeItem>> StoredAsync(string key)
    {
        var filter = Builders<LakeItem>.Filter.Where(item => item.ProjectId == projectId && item.Key == key);
        var sort = Builders<LakeItem>.Sort.Ascending(item => item.Version);
        return await fixture.Database.LakeItems.Find(filter).Sort(sort).ToListAsync(CancellationToken.None);
    }
}
