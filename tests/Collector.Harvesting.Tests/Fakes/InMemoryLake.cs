using System.Security.Cryptography;
using Collector.Core.Lake;
using Collector.Core.Platform;
using Collector.Lake;

namespace Collector.Harvesting.Tests.Fakes;

internal sealed class InMemoryLake : ILakeWriter
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, LakeItem> latest = [];

    public List<LakeWrite> Writes { get; } = [];

    public HashSet<string> Broken { get; } = [];

    public Task<LakeWriteResult> WriteAsync(LakeWrite write, LakeRetention retention, CancellationToken token)
    {
        lock (gate)
        {
            return Task.FromResult(Write(write, retention));
        }
    }

    private LakeWriteResult Write(LakeWrite write, LakeRetention retention)
    {
        Writes.Add(write);
        if (Broken.Contains(write.ProviderId)) throw new IOException("disk full");

        var key = new LakeKey(write.ProjectId, write.Source, write.Kind, write.ProviderId).ToString();
        var hash = Convert.ToHexStringLower(SHA256.HashData(write.Body));
        var previous = latest.GetValueOrDefault(key);
        if (previous is not null && retention == LakeRetention.ChangesOnly && previous.Hash == hash)
        {
            return new LakeWriteResult { Item = previous, NewVersion = false };
        }

        var version = retention == LakeRetention.LatestOnly ? 1 : (previous?.Version ?? 0) + 1;
        var item = new LakeItem
        {
            Id = Guid.NewGuid(),
            ProjectId = write.ProjectId,
            Key = key,
            Source = write.Source,
            Kind = write.Kind,
            ProviderId = write.ProviderId,
            Version = version,
            Url = write.Url,
            ContentType = write.ContentType,
            HarvestedAt = write.HarvestedAt,
            Hash = hash,
            Size = write.Body.Length,
        };
        latest[key] = item;
        return new LakeWriteResult { Item = item, NewVersion = true };
    }
}
