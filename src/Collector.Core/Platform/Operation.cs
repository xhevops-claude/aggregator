namespace Collector.Core.Platform;

public sealed record Operation
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid? ParentId { get; init; }
    public string Path { get; init; } = "";
    public string? Domain { get; init; }
    public Guid? SourceId { get; init; }
    public Dictionary<string, string> Settings { get; init; } = [];
    public List<OperationJob> Jobs { get; init; } = [];
}
