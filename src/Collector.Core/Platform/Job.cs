namespace Collector.Core.Platform;

public sealed record Job
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string Name { get; init; } = "";
    public List<JobCommand> Commands { get; init; } = [];
}
