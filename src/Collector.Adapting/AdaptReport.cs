using Collector.Core.Runs;

namespace Collector.Adapting;

public sealed record AdaptReport
{
    public RunItemState State { get; init; }
    public int RawItems { get; init; }
    public int Staged { get; init; }
    public int Rejected { get; init; }
    public List<Guid> StagingIds { get; init; } = [];
    public List<string> Skipped { get; init; } = [];
    public List<AdaptFailure> Failures { get; init; } = [];
}
