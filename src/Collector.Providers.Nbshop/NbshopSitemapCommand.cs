using System.Runtime.CompilerServices;
using Collector.Harvesting;

namespace Collector.Providers.Nbshop;

public sealed class NbshopSitemapCommand : IHarvestCommand
{
    private readonly TimeProvider time;

    public NbshopSitemapCommand() : this(TimeProvider.System)
    {
    }

    public NbshopSitemapCommand(TimeProvider time) => this.time = time;

    public string Name => NbshopNames.SitemapCommand;
    public string Platform => NbshopNames.Platform;

    public async IAsyncEnumerable<HarvestItem> RunAsync(
        HarvestRequest request, IFetcher fetcher, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var url = NbshopSource.SitemapUrl(request.Source);
        if (url is null)
        {
            yield return MissingRegion(request);
            yield break;
        }

        var outcome = await NbshopFetchOutcome.GetAsync(fetcher, url, cancellationToken);
        yield return outcome.ToItem(NbshopNames.SitemapKind, NbshopNames.SitemapProviderId, Now());
    }

    private HarvestItem MissingRegion(HarvestRequest request)
    {
        return new HarvestItem
        {
            Kind = NbshopNames.SitemapKind,
            ProviderId = NbshopNames.SitemapProviderId,
            Url = request.Source.BaseUrl,
            HarvestedAt = Now(),
            ErrorClass = "missing-setting",
            ErrorText = $"Source {request.Source.Key} has no {NbshopNames.SitemapRegionSetting} setting",
        };
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
