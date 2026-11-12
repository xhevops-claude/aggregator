using Collector.Harvesting.Tests.Fakes;

namespace Collector.Harvesting.Tests;

public class HarvestCommandCatalogTests
{
    [Fact]
    public void Finds_a_command_by_name_ignoring_case()
    {
        var pages = new ScriptedCommand("sportm.listing-pages", []);
        var catalog = new HarvestCommandCatalog([pages, new ScriptedCommand("sportm.product-detail", [])]);

        Assert.Same(pages, catalog.Find("SportM.Listing-Pages"));
        Assert.Same(pages, catalog.Get("sportm.listing-pages"));
        Assert.Equal(2, catalog.Names.Count);
    }

    [Fact]
    public void Rejects_two_commands_with_the_same_name()
    {
        var first = new ScriptedCommand("sportm.stock", []);
        var second = new ScriptedCommand("SPORTM.STOCK", []);

        var error = Assert.Throws<ArgumentException>(() => new HarvestCommandCatalog([first, second]));

        Assert.Contains("registered twice", error.Message);
    }

    [Fact]
    public void Rejects_a_command_without_a_name()
    {
        Assert.Throws<ArgumentException>(() => new HarvestCommandCatalog([new ScriptedCommand(" ", [])]));
    }

    [Fact]
    public void An_unknown_name_is_null_for_find_and_throws_for_get()
    {
        var catalog = new HarvestCommandCatalog([new ScriptedCommand("sportm.stock", [])]);

        Assert.Null(catalog.Find("nbshop.sitemap"));
        Assert.Throws<KeyNotFoundException>(() => catalog.Get("nbshop.sitemap"));
    }
}
