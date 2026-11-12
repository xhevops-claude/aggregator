using Collector.Adapting;
using Collector.Core.Domains;
using Collector.Core.Entities;
using Collector.Lake;

namespace Collector.Storing.Tests;

internal sealed class FakeAdapter(Func<RawItem, AdaptContext, IEnumerable<Entity>> adapt) : IAdapter
{
    public string Name { get; init; } = "fake.shoes";
    public string Platform { get; init; } = "sportm";
    public string Domain { get; init; } = DomainNames.Shoes;
    public IReadOnlyCollection<string> RawKinds { get; init; } = ["listing-page"];

    public IEnumerable<Entity> Adapt(RawItem raw, AdaptContext context) => adapt(raw, context);
}
