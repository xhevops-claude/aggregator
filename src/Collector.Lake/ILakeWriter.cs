using Collector.Core.Platform;

namespace Collector.Lake;

public interface ILakeWriter
{
    Task<LakeWriteResult> WriteAsync(LakeWrite write, LakeRetention retention, CancellationToken cancellationToken);
}
