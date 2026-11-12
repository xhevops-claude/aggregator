using Collector.Adapting;
using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Lake;

namespace Collector.Storing.Tests;

internal static class ShoeLines
{
    public static FakeAdapter Adapter() => new(Adapt);

    public static IEnumerable<Entity> Adapt(RawItem raw, AdaptContext context)
    {
        var parts = raw.Text.Split('|');
        var providerId = raw.Item.ProviderId;
        var productId = ProductIdentity.Candidate(context.ProjectSeed, context.Provider, providerId);

        yield return new Product
        {
            Id = productId,
            ProjectId = context.ProjectId,
            Namespace = context.Namespace,
            Provider = context.Provider,
            ProviderId = providerId,
            HarvestedAt = raw.Item.HarvestedAt,
            Name = raw.Text,
            Brand = parts[0],
            Sku = parts[1],
        };

        if (parts[0] == "boom") throw new FormatException($"Cannot read {raw.Item.Key}");

        yield return new Listing
        {
            Id = DeterministicId.For(context.ProjectSeed, context.Provider, "listing", providerId),
            ProjectId = context.ProjectId,
            Namespace = context.Namespace,
            Provider = context.Provider,
            ProviderId = providerId,
            HarvestedAt = raw.Item.HarvestedAt,
            ProductId = productId,
            Title = raw.Text,
            Url = raw.Item.Url,
            Availability = Availability.InStock,
            InfoCheckedAt = context.AdaptedAt,
        };
    }
}
