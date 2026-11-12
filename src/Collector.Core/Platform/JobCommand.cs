namespace Collector.Core.Platform;

public sealed record JobCommand
{
    public CommandStage Stage { get; init; }
    public string Name { get; init; } = "";
}
