namespace Collector.Core.Lake;

public sealed record LakeKey(Guid ProjectId, string Source, string Kind, string ProviderId)
{
    public override string ToString() => $"{ProjectId}/{Source}/{Kind}/{ProviderId}";
}
