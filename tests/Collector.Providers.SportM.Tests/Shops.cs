using Collector.Adapting;
using Collector.Core.Lake;
using Collector.Core.Platform;
using Collector.Harvesting;
using Collector.Lake;

namespace Collector.Providers.SportM.Tests;

internal static class Shops
{
    public static readonly Guid ProjectId = Guid.Parse("0b6c2f4e-6a59-4c1d-9e7f-2f3a1b4c5d6e");
    public static readonly Guid Seed = Guid.Parse("7d1e9a3c-2b4f-4e8a-9c6d-5f0b1a2c3d4e");
    public static readonly DateTime HarvestedAt = new(2026, 11, 12, 1, 31, 0, DateTimeKind.Utc);

    public static Source SportM { get; } = new()
    {
        Key = "sportm_mk",
        Provider = "sportm",
        BaseUrl = "https://sport-m.com.mk",
        Platform = "sportm",
        Currency = "MKD",
    };

    public static Source Intersport { get; } = new()
    {
        Key = "intersport_mk",
        Provider = "intersport",
        BaseUrl = "https://intersport.mk",
        Platform = "sportm",
        Currency = "MKD",
    };

    public static HarvestRequest Request(Source source)
    {
        return new HarvestRequest { ProjectId = ProjectId, Namespace = Namespace(source), Source = source };
    }

    public static AdaptContext Context(Source source)
    {
        return new AdaptContext
        {
            ProjectId = ProjectId,
            ProjectSeed = Seed,
            Namespace = Namespace(source),
            Provider = source.Provider,
            Source = source,
            AdaptedAt = HarvestedAt.AddMinutes(5),
        };
    }

    public static RawItem Raw(Source source, string kind, string providerId, string url, string sampleName)
    {
        var item = new LakeItem
        {
            ProjectId = ProjectId,
            Source = source.Key,
            Kind = kind,
            ProviderId = providerId,
            Url = url,
            HarvestedAt = HarvestedAt,
        };
        return new RawItem { Item = item, Body = SampleFiles.Bytes(sampleName) };
    }

    private static string Namespace(Source source) => $"clothing/shoes/{source.Provider}";
}
