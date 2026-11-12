namespace Collector.Harvesting;

public interface IFetcher
{
    Task<FetchResponse> GetAsync(Uri url, CancellationToken cancellationToken);
}
