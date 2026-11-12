using System.Runtime.CompilerServices;
using Collector.Harvesting;
using Collector.Providers.SportM.Parsing;

namespace Collector.Providers.SportM;

public sealed class SportMProductPageCommand(TimeProvider timeProvider) : IHarvestCommand
{
    public const string CommandName = "sportm.product-page";
    public const string ProductMissing = "product-missing";
    public const string ProductMismatch = "product-mismatch";

    public SportMProductPageCommand() : this(TimeProvider.System)
    {
    }

    public string Name => CommandName;
    public string Platform => SportMModule.Platform;

    public async IAsyncEnumerable<HarvestItem> RunAsync(
        HarvestRequest request, IFetcher fetcher, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var targets = request.Targets.AsEnumerable();
        if (request.Limit is { } limit) targets = targets.Take(Math.Max(limit, 0));

        const string kind = SportMRawKinds.ProductPage;
        foreach (var target in targets)
        {
            var productId = target.ProviderId.Trim();
            var url = SportMUrls.ProductPage(request.Source, productId);
            if (!string.IsNullOrWhiteSpace(target.Url)) url = SportMUrls.WithoutContext(target.Url.Trim());

            if (productId.Length == 0)
            {
                var now = timeProvider.GetUtcNow().UtcDateTime;
                yield return SportMItems.Failed(kind, "", url, "missing-id", "target has no provider id", now);
                continue;
            }

            var result = await SportMFetch.GetAsync(fetcher, url, timeProvider, cancellationToken);
            if (result.Failed)
            {
                yield return SportMItems.Failed(kind, productId, url, result);
                continue;
            }

            var headingId = ProductPageParser.HeadingId(result.Response!.Text);
            if (headingId is null)
            {
                var text = $"page for {productId} has no product heading";
                yield return SportMItems.Failed(kind, productId, url, ProductMissing, text, result.HarvestedAt);
                continue;
            }

            if (!string.Equals(headingId, productId, StringComparison.OrdinalIgnoreCase))
            {
                var text = $"requested {productId}, the page shows {headingId}";
                yield return SportMItems.Failed(kind, productId, url, ProductMismatch, text, result.HarvestedAt);
                continue;
            }

            yield return SportMItems.Stored(kind, productId, url, result);
        }
    }
}
