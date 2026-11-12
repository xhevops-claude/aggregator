using Collector.Core.Lake;

namespace Collector.Lake;

public interface ILakeReader
{
    Task<RawItem?> ReadLatestAsync(Guid projectId, string key, CancellationToken cancellationToken);
    Task<RawItem?> ReadAsync(Guid projectId, string key, int version, CancellationToken cancellationToken);
    Task<RawItem> ReadAsync(LakeItem item, CancellationToken cancellationToken);
    Task<IReadOnlyList<LakeItem>> LatestAsync(LakeQuery query, CancellationToken cancellationToken);
}
