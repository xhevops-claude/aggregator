using Collector.Core.Runs;

namespace Collector.Adapting;

internal sealed class AdaptTally
{
    public int RawItems { get; set; }
    public int Rejected { get; set; }
    public List<Guid> StagingIds { get; } = [];
    public List<string> Skipped { get; } = [];
    public List<AdaptFailure> Failures { get; } = [];

    public void Fail(string lakeKey, Exception exception) => Fail(lakeKey, exception.GetType().Name, exception.Message);

    public void Fail(string lakeKey, string errorClass, string errorText)
    {
        Failures.Add(new AdaptFailure { LakeKey = lakeKey, ErrorClass = errorClass, ErrorText = errorText });
    }

    public AdaptReport ToReport()
    {
        var state = Failures.Count == 0 ? RunItemState.Done : RunItemState.Failed;
        return new AdaptReport
        {
            State = state,
            RawItems = RawItems,
            Staged = StagingIds.Count,
            Rejected = Rejected,
            StagingIds = [.. StagingIds],
            Skipped = [.. Skipped],
            Failures = [.. Failures],
        };
    }
}
