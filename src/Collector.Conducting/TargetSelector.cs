using Collector.Core.Entities;
using Collector.Core.Messages;
using Collector.Core.Runs;
using Collector.Data;
using MongoDB.Driver;

namespace Collector.Conducting;

public sealed class TargetSelector(CollectorDatabase db)
{
    public const string ProductPageSuffix = ".product-page";
    public const int DefaultDetailTargets = 3;

    public async Task<List<HarvestTarget>?> SelectAsync(Run run, string command, int? limit, CancellationToken token)
    {
        if (!command.EndsWith(ProductPageSuffix, StringComparison.Ordinal)) return null;

        var filter = Builders<Listing>.Filter;
        var ofLeaf = filter.Eq(l => l.ProjectId, run.ProjectId) & filter.Eq(l => l.Namespace, run.Namespace);
        var withoutDetails = ofLeaf & filter.Eq(l => l.InfoCheckedAt, null);

        var take = limit ?? DefaultDetailTargets;
        var found = db.Listings.Find(withoutDetails).SortBy(l => l.ProviderId).Limit(take);
        var listings = await found.ToListAsync(token);
        return listings.Select(Target).ToList();
    }

    private static HarvestTarget Target(Listing listing)
    {
        return new HarvestTarget { ProviderId = listing.ProviderId, Url = listing.Url };
    }
}
