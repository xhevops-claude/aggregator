using System.Runtime.CompilerServices;

namespace Collector.Harvesting.Tests.Fakes;

internal sealed class FetchingCommand : IHarvestCommand
{
    public string Name => "shop.fetch";

    public string Platform => "test";

    public async IAsyncEnumerable<HarvestItem> RunAsync(HarvestRequest request, IFetcher fetcher,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var target in request.Targets)
        {
            var response = await fetcher.GetAsync(new Uri(target.Url!), cancellationToken);
            yield return new HarvestItem
            {
                Kind = "product-page",
                ProviderId = target.ProviderId,
                Url = response.Url.ToString(),
                ContentType = response.ContentType,
                Body = response.Body,
            };
        }
    }
}
