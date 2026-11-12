namespace Collector.Harvesting;

public sealed record FetcherOptions
{
    public const string DesktopChrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
        + "(KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36";

    public string UserAgent { get; init; } = DesktopChrome;
    public string AcceptLanguage { get; init; } = "mk,en;q=0.8";
    public string Accept { get; init; } = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(40);
    public int Attempts { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxRetryAfter { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan UnreachableRobotsHold { get; init; } = TimeSpan.FromMinutes(5);
    public HostSettings DefaultHost { get; init; } = new();
    public Dictionary<string, HostSettings> Hosts { get; init; } = [];
}
