using Collector.Core.Lake;

namespace Collector.Lake;

public sealed record LakeWriteResult
{
    public required LakeItem Item { get; init; }
    public bool NewVersion { get; init; }
}
