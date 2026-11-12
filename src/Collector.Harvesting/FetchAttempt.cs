namespace Collector.Harvesting;

internal sealed record FetchAttempt
{
    public FetchResponse? Response { get; init; }
    public TimeSpan? RetryAfter { get; init; }
    public Exception? Error { get; init; }
}
