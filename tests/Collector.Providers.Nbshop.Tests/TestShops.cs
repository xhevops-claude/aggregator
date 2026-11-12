using Collector.Adapting;
using Collector.Core.Ids;
using Collector.Core.Lake;
using Collector.Core.Platform;
using Collector.Harvesting;
using Collector.Lake;

namespace Collector.Providers.Nbshop.Tests;

public static class TestShops
{
    public static readonly Guid ProjectId = Guid.Parse("4d1c2b3a-5e6f-4a7b-8c9d-0e1f2a3b4c5d");
    public static readonly Guid ProjectSeed = Guid.Parse("9a8b7c6d-5e4f-4321-8fed-cba987654321");
    public static readonly DateTime HarvestedAt = new(2026, 11, 12, 8, 15, 0, DateTimeKind.Utc);
    public static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    public static Source SportVision { get; } = Shop("sportvision", "sportvision_mk", "https://www.sportvision.mk");
    public static Source SportReality { get; } = Shop("sportreality", "sportreality_mk", "https://www.sportreality.mk");
    public static Source Buzz { get; } = Shop("buzzsneakers", "buzzsneakers_mk", "https://www.buzzsneakers.mk");

    public static AdaptContext Context(Source source)
    {
        return new AdaptContext
        {
            ProjectId = ProjectId,
            ProjectSeed = ProjectSeed,
            Namespace = $"clothing/shoes/{source.Provider}",
            Provider = source.Provider,
            Source = source,
            AdaptedAt = Now.UtcDateTime,
        };
    }

    public static RawItem Raw(Source source, string kind, string providerId, string url, string sample)
    {
        var key = new LakeKey(ProjectId, source.Key, kind, providerId);
        var item = new LakeItem
        {
            ProjectId = ProjectId,
            Key = key.ToString(),
            Source = source.Key,
            Kind = kind,
            ProviderId = providerId,
            Version = 1,
            Url = url,
            ContentType = "text/html; charset=UTF-8",
            HarvestedAt = HarvestedAt,
        };
        return new RawItem { Item = item, Body = SampleFiles.Bytes(sample) };
    }

    public static HarvestRequest Request(Source source)
    {
        return new HarvestRequest
        {
            ProjectId = ProjectId,
            Namespace = $"clothing/shoes/{source.Provider}",
            Source = source,
        };
    }

    public static async Task<List<HarvestItem>> Collect(IAsyncEnumerable<HarvestItem> items)
    {
        var list = new List<HarvestItem>();
        await foreach (var item in items) list.Add(item);
        return list;
    }

    private static Source Shop(string provider, string key, string baseUrl)
    {
        return new Source
        {
            Id = DeterministicId.Create(ProjectId, $"source:{key}"),
            ProjectId = ProjectId,
            Key = key,
            Provider = provider,
            BaseUrl = baseUrl,
            Platform = NbshopNames.Platform,
            Currency = "MKD",
            Settings = new Dictionary<string, string> { [NbshopNames.SitemapRegionSetting] = "MAK_mk" },
        };
    }
}
