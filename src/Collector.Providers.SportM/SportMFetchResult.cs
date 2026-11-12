using Collector.Harvesting;

namespace Collector.Providers.SportM;

internal sealed record SportMFetchResult
{
    public FetchResponse? Response { get; init; }
    public string? ErrorClass { get; init; }
    public string? ErrorText { get; init; }
    public DateTime HarvestedAt { get; init; }

    public bool Failed => ErrorClass is not null;
}
