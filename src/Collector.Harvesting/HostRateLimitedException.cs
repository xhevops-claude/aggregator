namespace Collector.Harvesting;

public sealed class HostRateLimitedException : Exception
{
    public HostRateLimitedException(Uri url, DateTimeOffset until)
        : base($"{url.Authority} asked to wait until {until:u}, {url} was not requested")
    {
        Url = url;
        Until = until;
    }

    public Uri Url { get; }
    public DateTimeOffset Until { get; }
}
