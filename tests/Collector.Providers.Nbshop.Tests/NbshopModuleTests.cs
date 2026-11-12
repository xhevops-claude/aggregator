using Collector.Core.Domains;

namespace Collector.Providers.Nbshop.Tests;

public class NbshopModuleTests
{
    [Fact]
    public void Exposes_the_three_harvest_commands_of_the_platform()
    {
        var names = NbshopModule.HarvestCommands.Select(command => command.Name);

        Assert.Equal(["nbshop.sitemap", "nbshop.listing-pages", "nbshop.product-page"], names);
        Assert.All(NbshopModule.HarvestCommands, command => Assert.Equal("nbshop", command.Platform));
    }

    [Fact]
    public void Exposes_the_shoes_adapter_for_all_three_raw_kinds()
    {
        var adapter = Assert.Single(NbshopModule.Adapters);

        Assert.Equal("nbshop.shoes", adapter.Name);
        Assert.Equal("nbshop", adapter.Platform);
        Assert.Equal(DomainNames.Shoes, adapter.Domain);
        Assert.Equal(["sitemap", "listing-page", "product-page"], adapter.RawKinds);
    }
}
