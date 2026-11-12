using Collector.Harvesting;

namespace Collector.Providers.SportM.Tests;

internal static class HarvestRuns
{
    public static Task<List<HarvestItem>> CollectAsync(
        IHarvestCommand command, HarvestRequest request, IFetcher fetcher)
    {
        return CollectAsync(command, request, fetcher, CancellationToken.None);
    }

    public static async Task<List<HarvestItem>> CollectAsync(
        IHarvestCommand command, HarvestRequest request, IFetcher fetcher, CancellationToken cancellationToken)
    {
        var items = new List<HarvestItem>();
        await foreach (var item in command.RunAsync(request, fetcher, cancellationToken)) items.Add(item);
        return items;
    }
}
