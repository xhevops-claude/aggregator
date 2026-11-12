using Collector.Adapting;
using Collector.Core.Entities;
using Collector.Lake;

namespace Collector.Storing.Tests;

public class AdapterCatalogTests
{
    [Fact]
    public void Finds_the_adapter_by_platform_and_domain()
    {
        var sportm = Adapter("sportm.shoes", "sportm", "shoes");
        var nbshop = Adapter("nbshop.shoes", "nbshop", "shoes");
        var catalog = new AdapterCatalog([sportm, nbshop]);

        Assert.Same(nbshop, catalog.Get("nbshop", "shoes"));
        Assert.Same(sportm, catalog.Get("SportM", "Shoes"));
        Assert.Equal(2, catalog.All.Count);
    }

    [Fact]
    public void Two_adapters_for_one_platform_and_domain_are_rejected()
    {
        var first = Adapter("sportm.shoes", "sportm", "shoes");
        var second = Adapter("sportm.shoes.v2", "sportm", "shoes");

        var error = Assert.Throws<ArgumentException>(() => new AdapterCatalog([first, second]));

        Assert.Contains("sportm.shoes.v2", error.Message);
    }

    [Fact]
    public void Unknown_platform_or_domain_is_not_found()
    {
        var catalog = new AdapterCatalog([Adapter("sportm.shoes", "sportm", "shoes")]);

        Assert.False(catalog.TryGet("sportm", "electronics", out _));
        Assert.Throws<KeyNotFoundException>(() => catalog.Get("nbshop", "shoes"));
    }

    private static FakeAdapter Adapter(string name, string platform, string domain)
    {
        return new FakeAdapter(NoEntities) { Name = name, Platform = platform, Domain = domain };
    }

    private static IEnumerable<Entity> NoEntities(RawItem raw, AdaptContext context) => [];
}
