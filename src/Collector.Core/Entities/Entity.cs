namespace Collector.Core.Entities;

public abstract record Entity
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string Namespace { get; init; } = "";
    public string Provider { get; init; } = "";
    public string ProviderId { get; init; } = "";
    public string SourceUrl { get; init; } = "";
    public DateTime HarvestedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
