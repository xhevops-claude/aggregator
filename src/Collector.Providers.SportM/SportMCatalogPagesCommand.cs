using System.Runtime.CompilerServices;
using Collector.Harvesting;
using Collector.Providers.SportM.Parsing;

namespace Collector.Providers.SportM;

public sealed class SportMCatalogPagesCommand(TimeProvider timeProvider) : IHarvestCommand
{
    public const string CommandName = "sportm.catalog-pages";
    private const int MaxFailuresInARow = 3;

    public SportMCatalogPagesCommand() : this(TimeProvider.System)
    {
    }

    public string Name => CommandName;
    public string Platform => SportMModule.Platform;

    public async IAsyncEnumerable<HarvestItem> RunAsync(
        HarvestRequest request, IFetcher fetcher, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var plan = SportMCatalogQueries.Resolve(request);
        if (plan.ErrorClass is not null)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var kind = SportMRawKinds.CatalogPage;
            yield return SportMItems.Failed(kind, plan.ErrorProviderId, "", plan.ErrorClass, plan.ErrorText, now);
            yield break;
        }

        var pagesLeft = request.Limit ?? int.MaxValue;
        foreach (var query in plan.Queries)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var failuresInARow = 0;
            for (var page = 1; pagesLeft > 0; page++)
            {
                pagesLeft--;
                var url = SportMUrls.CatalogPage(request.Source, query, page);
                var providerId = $"{query}/page-{page}";
                var result = await SportMFetch.GetAsync(fetcher, url, timeProvider, cancellationToken);

                if (result.Failed)
                {
                    yield return SportMItems.Failed(SportMRawKinds.CatalogPage, providerId, url, result);
                    failuresInARow++;
                    if (EndsWalk(result.ErrorClass!) || failuresInARow >= MaxFailuresInARow) break;
                    continue;
                }

                failuresInARow = 0;
                yield return SportMItems.Stored(SportMRawKinds.CatalogPage, providerId, url, result);
                if (CountNewTiles(result, seen) == 0) break;
            }
        }
    }

    private static bool EndsWalk(string errorClass)
    {
        return errorClass == "http-404" || errorClass == SportMFetch.Robots;
    }

    private static int CountNewTiles(SportMFetchResult result, HashSet<string> seen)
    {
        var ids = CatalogPageParser.ProductIds(result.Response!.Text);
        return ids.Count(seen.Add);
    }
}
