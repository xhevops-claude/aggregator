using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Collector.Core.Ids;
using Collector.Core.Lake;
using Collector.Core.Platform;
using MongoDB.Driver;

namespace Collector.Lake.Tests;

[Trait("Category", "Integration")]
public class RoundTripTests(LakeFixture fixture) : IClassFixture<LakeFixture>
{
    private readonly LakeStore lake = fixture.NewStore();
    private readonly Guid projectId = Guid.NewGuid();

    private static CancellationToken Token => CancellationToken.None;

    [Fact]
    public async Task Binary_body_round_trips_byte_for_byte()
    {
        var body = BinaryBody();
        var write = Writes.For(projectId, "4711", body);

        var written = await lake.WriteAsync(write, LakeRetention.FullHistory, Token);
        var read = await lake.ReadLatestAsync(projectId, Writes.KeyOf(write), Token);

        Assert.NotNull(read);
        Assert.Equal(body, read.Body);
        Assert.Equal(body.LongLength, written.Item.Size);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(body)), written.Item.Hash);
    }

    [Fact]
    public async Task Empty_body_round_trips()
    {
        var write = Writes.For(projectId, "4711", []);

        var written = await lake.WriteAsync(write, LakeRetention.FullHistory, Token);
        var read = await lake.ReadAsync(written.Item, Token);

        Assert.Empty(read.Body);
        Assert.Equal(0, written.Item.Size);
    }

    [Fact]
    public async Task Body_is_gzip_on_disk_under_project_source_kind_provider_id_and_version()
    {
        var body = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("<li>adidas VS PACE 2.0</li>", 200)));
        var write = Writes.For(projectId, "HP6005", body);
        var segment = LakePath.HashSuffixedSegment("HP6005");

        var item = (await lake.WriteAsync(write, LakeRetention.FullHistory, Token)).Item;

        var expected = Path.Combine(lake.Root, projectId.ToString(), "sportm_mk", "product", segment, "000001.gz");
        Assert.Equal(Path.GetFullPath(expected), lake.PathOf(item));
        Assert.Equal($"{projectId}/sportm_mk/product/{segment}/000001.gz", item.Location);
        var onDisk = await File.ReadAllBytesAsync(lake.PathOf(item), Token);
        Assert.Equal(new byte[] { 0x1F, 0x8B }, onDisk[..2]);
        Assert.True(onDisk.Length < body.Length);
        Assert.Equal(body, Decompress(onDisk));
    }

    [Fact]
    public async Task Index_document_carries_the_write_and_a_deterministic_id()
    {
        var write = Writes.For(projectId, "HP6005", "<html>HP6005</html>");
        var key = Writes.KeyOf(write);

        var item = (await lake.WriteAsync(write, LakeRetention.FullHistory, Token)).Item;
        var stored = await fixture.Database.LakeItems.Find(i => i.Id == item.Id).SingleAsync(Token);

        Assert.Equal(DeterministicId.Create(projectId, $"lake:{key}:1"), item.Id);
        Assert.Equal(new LakeKey(projectId, "sportm_mk", "product", "HP6005").ToString(), item.Key);
        Assert.Equal((projectId, "sportm_mk", "product"), (item.ProjectId, item.Source, item.Kind));
        Assert.Equal("HP6005", item.ProviderId);
        Assert.Equal((write.Url, write.ContentType, write.HarvestedAt), (item.Url, item.ContentType, item.HarvestedAt));
        Assert.Equal(item, stored);
    }

    [Fact]
    public async Task Harvest_time_is_kept_in_utc_to_the_millisecond()
    {
        var local = new DateTime(2026, 11, 12, 10, 30, 0, 123, DateTimeKind.Local).AddTicks(4567);
        var write = Writes.For(projectId, "4711", "<html/>") with { HarvestedAt = local };

        var item = (await lake.WriteAsync(write, LakeRetention.FullHistory, Token)).Item;
        var stored = await fixture.Database.LakeItems.Find(i => i.Id == item.Id).SingleAsync(Token);

        var expected = local.ToUniversalTime().AddTicks(-4567);
        Assert.Equal(DateTimeKind.Utc, item.HarvestedAt.Kind);
        Assert.Equal(expected, item.HarvestedAt);
        Assert.Equal(item.HarvestedAt, stored.HarvestedAt);
    }

    [Fact]
    public async Task Reads_by_version_by_latest_and_by_item()
    {
        var a = Writes.For(projectId, "4711", "<html>a</html>");
        var b = Writes.For(projectId, "4711", "<html>b</html>");
        var key = Writes.KeyOf(a);

        var first = await lake.WriteAsync(a, LakeRetention.FullHistory, Token);
        await lake.WriteAsync(b, LakeRetention.FullHistory, Token);

        Assert.Equal("<html>a</html>", (await lake.ReadAsync(projectId, key, 1, Token))?.Text);
        Assert.Equal("<html>b</html>", (await lake.ReadAsync(projectId, key, 2, Token))?.Text);
        Assert.Equal("<html>b</html>", (await lake.ReadLatestAsync(projectId, key, Token))?.Text);
        Assert.Equal("<html>a</html>", (await lake.ReadAsync(first.Item, Token)).Text);
        Assert.Null(await lake.ReadAsync(projectId, key, 3, Token));
        Assert.Null(await lake.ReadLatestAsync(projectId, key + "-missing", Token));
        Assert.Null(await lake.ReadLatestAsync(Guid.NewGuid(), key, Token));
    }

    [Fact]
    public async Task A_body_that_no_longer_matches_its_hash_is_refused()
    {
        var write = Writes.For(projectId, "4711", "<html>original</html>");
        var item = (await lake.WriteAsync(write, LakeRetention.FullHistory, Token)).Item;

        await File.WriteAllBytesAsync(lake.PathOf(item), Compress("<html>tampered</html>"u8.ToArray()), Token);

        await Assert.ThrowsAsync<InvalidDataException>(() => lake.ReadAsync(item, Token));
    }

    [Fact]
    public async Task Writes_without_project_source_kind_or_provider_id_are_rejected()
    {
        var valid = Writes.For(projectId, "4711", "<html/>");
        var invalid = new[]
        {
            valid with { ProjectId = Guid.Empty },
            valid with { Source = "" },
            valid with { Kind = "" },
            valid with { ProviderId = "" },
            valid with { Source = "sportm/mk" },
            valid with { Kind = "product/page" },
        };

        foreach (var write in invalid)
        {
            var attempt = () => lake.WriteAsync(write, LakeRetention.FullHistory, Token);
            await Assert.ThrowsAnyAsync<ArgumentException>(attempt);
        }

        Assert.False(Directory.Exists(Path.Combine(lake.Root, projectId.ToString())));
    }

    private static byte[] BinaryBody()
    {
        var everyByte = Enumerable.Range(0, 256).Select(value => (byte)value);
        var brokenUtf8 = new byte[] { 0xC3, 0x28, 0xA0, 0xA1, 0xE2, 0x28, 0xA1, 0xF0, 0x28, 0x8C, 0xBC, 0xFF, 0xFE };
        var noise = new byte[200_000];
        new Random(4711).NextBytes(noise);
        return everyByte.Concat(brokenUtf8).Concat(noise).ToArray();
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using var gzip = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var body = new MemoryStream();
        gzip.CopyTo(body);
        return body.ToArray();
    }

    private static byte[] Compress(byte[] body)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(body);
        }

        return compressed.ToArray();
    }
}
