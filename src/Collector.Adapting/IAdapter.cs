using Collector.Core.Entities;
using Collector.Lake;

namespace Collector.Adapting;

public interface IAdapter
{
    string Name { get; }
    string Platform { get; }
    string Domain { get; }
    IReadOnlyCollection<string> RawKinds { get; }

    IEnumerable<Entity> Adapt(RawItem raw, AdaptContext context);
}
