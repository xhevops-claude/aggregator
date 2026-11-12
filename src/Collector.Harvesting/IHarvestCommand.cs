namespace Collector.Harvesting;

public interface IHarvestCommand
{
    string Name { get; }
    string Platform { get; }

    IAsyncEnumerable<HarvestItem> RunAsync(
        HarvestRequest request, IFetcher fetcher, CancellationToken cancellationToken);
}
