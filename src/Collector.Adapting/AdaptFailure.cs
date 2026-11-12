namespace Collector.Adapting;

public sealed record AdaptFailure
{
    public string LakeKey { get; init; } = "";
    public string ErrorClass { get; init; } = "";
    public string ErrorText { get; init; } = "";
}
