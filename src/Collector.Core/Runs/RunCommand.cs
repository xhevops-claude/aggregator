using Collector.Core.Platform;

namespace Collector.Core.Runs;

public sealed record RunCommand
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public int Order { get; init; }
    public CommandStage Stage { get; init; }
    public string Name { get; init; } = "";
    public RunState State { get; init; }
}
