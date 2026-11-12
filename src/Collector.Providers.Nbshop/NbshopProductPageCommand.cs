using System.Runtime.CompilerServices;
using Collector.Core.Messages;
using Collector.Harvesting;

namespace Collector.Providers.Nbshop;

public sealed class NbshopProductPageCommand : IHarvestCommand
{
    private readonly TimeProvider time;

    public NbshopProductPageCommand() : this(TimeProvider.System)
    {
    }

    public NbshopProductPageCommand(TimeProvider time) => this.time = time;

    public string Name => NbshopNames.ProductPageCommand;
    public string Platform => NbshopNames.Platform;

    public async IAsyncEnumerable<HarvestItem> RunAsync(
        HarvestRequest request, IFetcher fetcher, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var targets = request.Limit is int limit ? request.Targets.Take(limit) : request.Targets;
        foreach (var target in targets)
        {
            yield return await HarvestAsync(request, target, fetcher, cancellationToken);
        }
    }

    private async Task<HarvestItem> HarvestAsync(
        HarvestRequest request, HarvestTarget target, IFetcher fetcher, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.Url)) return MissingUrl(target);

        var url = NbshopUrl.Resolve(NbshopSource.BaseUrl(request.Source), target.Url);
        var providerId = ProviderIdOf(target, url);
        var outcome = await NbshopFetchOutcome.GetAsync(fetcher, url, cancellationToken);
        return outcome.ToItem(NbshopNames.ProductPageKind, providerId, Now());
    }

    private static string ProviderIdOf(HarvestTarget target, string url)
    {
        if (!string.IsNullOrWhiteSpace(target.ProviderId)) return target.ProviderId.Trim();

        return NbshopProductUrl.TryParse(url, out var product) ? product.ProviderId : url;
    }

    private HarvestItem MissingUrl(HarvestTarget target)
    {
        return new HarvestItem
        {
            Kind = NbshopNames.ProductPageKind,
            ProviderId = target.ProviderId,
            Url = "",
            HarvestedAt = Now(),
            ErrorClass = "missing-url",
            ErrorText = $"Target {target.ProviderId} has no product URL",
        };
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
