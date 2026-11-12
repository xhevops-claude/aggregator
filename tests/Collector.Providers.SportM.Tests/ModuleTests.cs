namespace Collector.Providers.SportM.Tests;

public sealed class ModuleTests
{
    [Fact]
    public void ListsTheCommandsAndTheAdapter()
    {
        var names = SportMModule.HarvestCommands.Select(command => command.Name);

        Assert.Equal(["sportm.catalog-pages", "sportm.product-page"], names);
        Assert.All(SportMModule.HarvestCommands, command => Assert.Equal("sportm", command.Platform));
        Assert.Equal("sportm.shoes", Assert.Single(SportMModule.Adapters).Name);
    }
}
