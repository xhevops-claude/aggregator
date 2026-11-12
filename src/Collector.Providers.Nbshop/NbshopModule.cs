using Collector.Adapting;
using Collector.Harvesting;

namespace Collector.Providers.Nbshop;

public static class NbshopModule
{
    public static IReadOnlyList<IHarvestCommand> HarvestCommands { get; } =
    [
        new NbshopSitemapCommand(),
        new NbshopListingPagesCommand(),
        new NbshopProductPageCommand(),
    ];

    public static IReadOnlyList<IAdapter> Adapters { get; } = [new NbshopShoesAdapter()];
}
