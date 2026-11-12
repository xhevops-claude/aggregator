using Collector.Core.Messages;
using Collector.Core.Platform;

namespace Collector.Adapting;

public sealed record AdaptInput
{
    public required AdaptCommand Command { get; init; }
    public required Operation Operation { get; init; }
    public required string Domain { get; init; }
    public required Source Source { get; init; }
    public required Project Project { get; init; }
    public required IAdapter Adapter { get; init; }
}
