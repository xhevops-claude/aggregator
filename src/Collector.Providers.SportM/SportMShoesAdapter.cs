using Collector.Adapting;
using Collector.Core.Domains;
using Collector.Core.Entities;
using Collector.Lake;
using Collector.Providers.SportM.Mapping;

namespace Collector.Providers.SportM;

public sealed class SportMShoesAdapter : IAdapter
{
    public const string AdapterName = "sportm.shoes";

    public string Name => AdapterName;
    public string Platform => SportMModule.Platform;
    public string Domain => DomainNames.Shoes;
    public IReadOnlyCollection<string> RawKinds { get; } = [SportMRawKinds.CatalogPage, SportMRawKinds.ProductPage];

    public IEnumerable<Entity> Adapt(RawItem raw, AdaptContext context)
    {
        if (raw.Item.Kind == SportMRawKinds.CatalogPage) return CatalogPageMapper.Map(raw, context);
        if (raw.Item.Kind == SportMRawKinds.ProductPage) return ProductPageMapper.Map(raw, context);
        return [];
    }
}
