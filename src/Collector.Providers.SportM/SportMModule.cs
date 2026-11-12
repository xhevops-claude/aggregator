using Collector.Adapting;
using Collector.Harvesting;

namespace Collector.Providers.SportM;

public static class SportMModule
{
    public const string Platform = "sportm";

    public static IReadOnlyList<IHarvestCommand> HarvestCommands { get; } =
        [new SportMCatalogPagesCommand(), new SportMProductPageCommand()];

    public static IReadOnlyList<IAdapter> Adapters { get; } = [new SportMShoesAdapter()];
}
