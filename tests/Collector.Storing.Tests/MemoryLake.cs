using System.Text;
using Collector.Core.Lake;
using Collector.Lake;

namespace Collector.Storing.Tests;

internal sealed class MemoryLake : ILakeReader
{
    private readonly List<RawItem> items = [];

    public LakeQuery? LastQuery { get; private set; }

    public RawItem Add(Guid projectId, string source, string kind, string providerId, string body, int version = 1)
    {
        var item = new LakeItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Key = new LakeKey(projectId, source, kind, providerId).ToString(),
            Source = source,
            Kind = kind,
            ProviderId = providerId,
            Version = version,
            Url = $"https://{source}.example/{providerId}",
            ContentType = "text/plain",
            HarvestedAt = FixedTime.Start,
        };
        var raw = new RawItem { Item = item, Body = Encoding.UTF8.GetBytes(body) };
        items.Add(raw);
        return raw;
    }

    public Task<RawItem?> ReadLatestAsync(Guid projectId, string key, CancellationToken cancellationToken)
    {
        var raw = Latest(projectId).FirstOrDefault(candidate => candidate.Item.Key == key);
        return Task.FromResult(raw);
    }

    public Task<RawItem?> ReadAsync(Guid projectId, string key, int version, CancellationToken cancellationToken)
    {
        var raw = items.FirstOrDefault(candidate => IsVersion(candidate, projectId, key, version));
        return Task.FromResult(raw);
    }

    public Task<RawItem> ReadAsync(LakeItem item, CancellationToken cancellationToken)
    {
        var raw = items.First(candidate => IsVersion(candidate, item.ProjectId, item.Key, item.Version));
        return Task.FromResult(raw);
    }

    public Task<IReadOnlyList<LakeItem>> LatestAsync(LakeQuery query, CancellationToken cancellationToken)
    {
        LastQuery = query;
        var latest = Latest(query.ProjectId);
        var matching = latest.Where(raw => raw.Item.Source == query.Source && query.Kinds.Contains(raw.Item.Kind));
        var found = matching.Select(raw => raw.Item).ToList();
        return Task.FromResult<IReadOnlyList<LakeItem>>(found);
    }

    private IEnumerable<RawItem> Latest(Guid projectId)
    {
        var ofProject = items.Where(raw => raw.Item.ProjectId == projectId);
        return ofProject.GroupBy(raw => raw.Item.Key).Select(group => group.MaxBy(raw => raw.Item.Version)!);
    }

    private static bool IsVersion(RawItem raw, Guid projectId, string key, int version)
    {
        return raw.Item.ProjectId == projectId && raw.Item.Key == key && raw.Item.Version == version;
    }
}
