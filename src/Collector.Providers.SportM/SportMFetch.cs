using Collector.Harvesting;

namespace Collector.Providers.SportM;

internal static class SportMFetch
{
    public const string Robots = "robots";

    public static async Task<SportMFetchResult> GetAsync(
        IFetcher fetcher, string url, TimeProvider time, CancellationToken cancellationToken)
    {
        try
        {
            var response = await fetcher.GetAsync(new Uri(url), cancellationToken);
            var harvestedAt = time.GetUtcNow().UtcDateTime;
            if (response.IsSuccess) return new SportMFetchResult { Response = response, HarvestedAt = harvestedAt };

            return new SportMFetchResult
            {
                Response = response,
                ErrorClass = $"http-{response.StatusCode}",
                ErrorText = $"HTTP {response.StatusCode} for {url}",
                HarvestedAt = harvestedAt,
            };
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new SportMFetchResult
            {
                ErrorClass = ClassOf(exception),
                ErrorText = exception.Message,
                HarvestedAt = time.GetUtcNow().UtcDateTime,
            };
        }
    }

    public static string ClassOf(Exception exception)
    {
        var name = exception.GetType().Name;
        if (name.Contains("Robots", StringComparison.OrdinalIgnoreCase)) return Robots;
        if (exception is HttpRequestException { StatusCode: { } status }) return $"http-{(int)status}";
        return name;
    }
}
