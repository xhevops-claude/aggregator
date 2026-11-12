using Collector.Core.Platform;

namespace Collector.Lake.Tests;

[Trait("Category", "Integration")]
public class ProviderIdTests(LakeFixture fixture) : IClassFixture<LakeFixture>
{
    private readonly LakeStore lake = fixture.NewStore();
    private readonly Guid projectId = Guid.NewGuid();

    private static CancellationToken Token => CancellationToken.None;

    [Fact]
    public async Task Nasty_provider_ids_each_get_their_own_folder_and_round_trip()
    {
        var results = new List<(string ProviderId, LakeWriteResult Result)>();
        foreach (var providerId in NastyIds.All)
        {
            var write = Writes.For(projectId, providerId, $"body of {providerId}");
            results.Add((providerId, await lake.WriteAsync(write, LakeRetention.FullHistory, Token)));
        }

        foreach (var (providerId, result) in results)
        {
            var key = Writes.KeyOf(Writes.For(projectId, providerId, ""));
            var read = await lake.ReadLatestAsync(projectId, key, Token);

            Assert.NotNull(read);
            Assert.Equal($"body of {providerId}", read.Text);
            Assert.Equal(providerId, read.Item.ProviderId);
            Assert.Equal(result.Item, read.Item);
        }

        var kindFolder = Path.Combine(lake.Root, projectId.ToString(), Writes.Source, Writes.Kind);
        var folders = Directory.GetDirectories(kindFolder).Select(Path.GetFileName).OfType<string>().ToList();
        Assert.Equal(NastyIds.All.Count, folders.Count);
        Assert.Equal(folders.Count, folders.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(folders, folder => Assert.Matches("^[A-Za-z0-9][A-Za-z0-9_-]*$", folder));
    }

    [Fact]
    public async Task Ids_that_differ_only_in_case_never_share_a_body()
    {
        var upper = Writes.For(projectId, "HP6005", "upper");
        var lower = Writes.For(projectId, "hp6005", "lower");

        var first = await lake.WriteAsync(upper, LakeRetention.LatestOnly, Token);
        var second = await lake.WriteAsync(lower, LakeRetention.LatestOnly, Token);

        Assert.NotEqual(first.Item.Location, second.Item.Location, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("upper", (await lake.ReadLatestAsync(projectId, Writes.KeyOf(upper), Token))?.Text);
        Assert.Equal("lower", (await lake.ReadLatestAsync(projectId, Writes.KeyOf(lower), Token))?.Text);
    }
}
