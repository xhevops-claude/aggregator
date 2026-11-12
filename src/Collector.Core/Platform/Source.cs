namespace Collector.Core.Platform;

public sealed record Source
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid ProviderId { get; init; }
    public string Key { get; init; } = "";
    public string Provider { get; init; } = "";
    public string BaseUrl { get; init; } = "";
    public string Platform { get; init; } = "";
    public string Currency { get; init; } = "";
    public TimeSpan MinRequestInterval { get; init; } = TimeSpan.FromSeconds(1);
    public bool RespectRobots { get; init; } = true;
    public Dictionary<string, string> Settings { get; init; } = [];
}
