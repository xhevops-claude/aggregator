using Collector.Core.Messages;
using Collector.Core.Platform;

namespace Collector.Messaging.Tests;

internal static class TestMessages
{
    public static readonly Guid ProjectId = Guid.Parse("6f1d2c3b-4a59-4e7d-8c1b-2a3f4e5d6c7b");
    public static readonly Guid RunId = Guid.Parse("1a2b3c4d-5e6f-4a1b-9c2d-3e4f5a6b7c8d");

    public static HarvestCommand HarvestCommand(string command = "sportm.product-detail")
    {
        return new HarvestCommand
        {
            ProjectId = ProjectId,
            RunId = RunId,
            ItemId = Guid.NewGuid(),
            Namespace = "clothing/shoes/sportm",
            Command = command,
            Source = "sportm_mk",
            Targets = [new HarvestTarget { ProviderId = "4711", Url = "https://example.test/p/4711" }],
            Parameters = new Dictionary<string, string> { ["brand"] = "adidas" },
            Limit = 3,
            Mechanism = Mechanism.Browser,
            Pacing = new Pacing { Mode = PacingMode.Jitter, Jitter = TimeSpan.FromSeconds(30) },
        };
    }

    public static AdaptCommand AdaptCommand()
    {
        return new AdaptCommand
        {
            ProjectId = ProjectId,
            RunId = RunId,
            ItemId = Guid.NewGuid(),
            Namespace = "clothing/shoes/sportm",
            Command = "sportm.products",
            LakeKeys = ["sportm_mk/product/4711"],
        };
    }

    public static LakeItemStored LakeItemStored()
    {
        return new LakeItemStored
        {
            ProjectId = ProjectId,
            RunId = RunId,
            ItemId = Guid.NewGuid(),
            Key = "sportm_mk/product/4711",
            Version = 2,
            Hash = "sha256:abc",
            Size = 1024,
        };
    }
}
