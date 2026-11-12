using Collector.Core.Platform;

namespace Collector.Harvesting;

public interface ISourceFetcher : IFetcher
{
    void Register(Source source);
}
