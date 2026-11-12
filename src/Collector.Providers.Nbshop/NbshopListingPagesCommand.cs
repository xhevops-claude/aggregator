using System.Runtime.CompilerServices;
using Collector.Harvesting;

namespace Collector.Providers.Nbshop;

public sealed class NbshopListingPagesCommand : IHarvestCommand
{
    private const int NotFound = 404;

    private readonly TimeProvider time;

    public NbshopListingPagesCommand() : this(TimeProvider.System)
    {
    }

    public NbshopListingPagesCommand(TimeProvider time) => this.time = time;

    public string Name => NbshopNames.ListingPagesCommand;
    public string Platform => NbshopNames.Platform;

    public async IAsyncEnumerable<HarvestItem> RunAsync(
        HarvestRequest request, IFetcher fetcher, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var listing = ListingOf(request);
        if (listing is null)
        {
            yield return MissingListing(request);
            yield break;
        }

        var baseUrl = NbshopSource.BaseUrl(request.Source);
        var language = NbshopSource.Language(request.Source);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var yielded = 0;

        for (var page = 1; request.Limit is null || yielded < request.Limit; page++)
        {
            var url = listing.PageUrl(baseUrl, language, page);
            var providerId = listing.ProviderId(page);
            var outcome = await NbshopFetchOutcome.GetAsync(fetcher, url, cancellationToken);

            if (page > 1 && outcome.StatusCode == NotFound) yield break;
            if (!outcome.Succeeded || outcome.Response is null)
            {
                yield return outcome.ToItem(NbshopNames.ListingPageKind, providerId, Now());
                yield break;
            }

            var parsed = NbshopListingPage.Parse(outcome.Response.Text, outcome.Response.Url);
            var newTiles = 0;
            foreach (var tile in parsed.Tiles)
            {
                if (seen.Add(tile.ProviderId)) newTiles++;
            }

            if (page > 1 && newTiles == 0) yield break;

            yield return outcome.ToItem(NbshopNames.ListingPageKind, providerId, Now());
            yielded++;

            if (!parsed.LinksToPage(page + 1)) yield break;
        }
    }

    private static NbshopListingPath? ListingOf(HarvestRequest request)
    {
        var listing = request.Parameter(HarvestParameters.Listing);
        if (!string.IsNullOrWhiteSpace(listing)) return NbshopListingPath.Parse(listing);

        return NbshopListingPath.ForBrand(request.Parameter(HarvestParameters.Brand));
    }

    private HarvestItem MissingListing(HarvestRequest request)
    {
        return new HarvestItem
        {
            Kind = NbshopNames.ListingPageKind,
            ProviderId = HarvestParameters.Listing,
            Url = request.Source.BaseUrl,
            HarvestedAt = Now(),
            ErrorClass = "missing-parameter",
            ErrorText = $"{Name} needs a {HarvestParameters.Listing} or a {HarvestParameters.Brand} parameter",
        };
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
