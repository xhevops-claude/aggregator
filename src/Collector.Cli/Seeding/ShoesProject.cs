using Collector.Core.Domains;
using Collector.Core.Ids;
using Collector.Core.Platform;

namespace Collector.Cli.Seeding;

public static class ShoesProject
{
    public const string Name = "mk-shoes";
    public const string ShoesPath = "clothing/shoes";

    private static readonly Guid Root = Guid.Parse("9c3f4a51-6d2e-4b8a-a7c1-0e5d3f2b1a90");

    public static Guid Id { get; } = DeterministicId.Create(Root, $"project:{Name}");

    public static IReadOnlyList<ShopSeed> Shops { get; } =
    [
        new("sportvision", "Sport Vision MK", "sportvision_mk", "https://www.sportvision.mk", "nbshop"),
        new("sportreality", "Sport Reality MK", "sportreality_mk", "https://www.sportreality.mk", "nbshop"),
        new("buzzsneakers", "Buzz Sneakers MK", "buzzsneakers_mk", "https://www.buzzsneakers.mk", "nbshop"),
        new("sportm", "Sport M", "sportm_mk", "https://sport-m.com.mk", "sportm"),
        new("intersport", "Intersport MK", "intersport_mk", "https://intersport.mk", "sportm"),
    ];

    public static Project Project() => new()
    {
        Id = Id,
        Name = Name,
        Seed = Id,
        LakeRetention = LakeRetention.ChangesOnly,
    };

    public static Provider Provider(ShopSeed shop) => new()
    {
        Id = IdFor("provider", shop.Provider),
        ProjectId = Id,
        Key = shop.Provider,
        Name = shop.Name,
    };

    public static Source Source(ShopSeed shop)
    {
        var settings = new Dictionary<string, string>();
        if (shop.Platform == "nbshop") settings["sitemapRegion"] = "MAK_mk";

        return new Source
        {
            Id = IdFor("source", shop.SourceKey),
            ProjectId = Id,
            ProviderId = IdFor("provider", shop.Provider),
            Key = shop.SourceKey,
            Provider = shop.Provider,
            BaseUrl = shop.BaseUrl,
            Platform = shop.Platform,
            Currency = "MKD",
            Settings = settings,
        };
    }

    public static IReadOnlyList<Job> Jobs() =>
    [
        NewJob("nbshop.discover", Harvest("nbshop.listing-pages"), Adapt("nbshop.shoes")),
        NewJob("nbshop.sitemap", Harvest("nbshop.sitemap"), Adapt("nbshop.shoes")),
        NewJob("nbshop.details", Harvest("nbshop.product-page"), Adapt("nbshop.shoes")),
        NewJob("sportm.discover", Harvest("sportm.catalog-pages"), Adapt("sportm.shoes")),
        NewJob("sportm.details", Harvest("sportm.product-page"), Adapt("sportm.shoes")),
    ];

    public static Strategy DevStrategy() => new()
    {
        Id = IdFor("strategy", "dev"),
        ProjectId = Id,
        Name = "dev",
        Mechanism = Mechanism.Http,
        Pacing = new Pacing { Mode = PacingMode.Interval, Interval = TimeSpan.FromSeconds(1) },
        Adapt = AdaptMode.Batch,
    };

    public static IReadOnlyList<Operation> Operations()
    {
        var clothing = new Operation { Id = IdFor("operation", "clothing"), ProjectId = Id, Path = "clothing" };
        var shoes = new Operation
        {
            Id = IdFor("operation", ShoesPath),
            ProjectId = Id,
            ParentId = clothing.Id,
            Path = ShoesPath,
            Domain = DomainNames.Shoes,
        };

        var operations = new List<Operation> { clothing, shoes };
        foreach (var shop in Shops) operations.Add(Leaf(shop, shoes.Id));
        return operations;
    }

    public static Guid IdFor(string kind, string key) => DeterministicId.Create(Id, $"{kind}:{key}");

    private static Operation Leaf(ShopSeed shop, Guid parentId)
    {
        var strategyId = IdFor("strategy", "dev");
        var jobs = Jobs().Where(job => job.Name.StartsWith(shop.Platform + ".")).ToList();
        var pairs = jobs.Select(job => new OperationJob { JobId = job.Id, StrategyId = strategyId }).ToList();
        var path = $"{ShoesPath}/{shop.Provider}";

        return new Operation
        {
            Id = IdFor("operation", path),
            ProjectId = Id,
            ParentId = parentId,
            Path = path,
            SourceId = IdFor("source", shop.SourceKey),
            Jobs = pairs,
        };
    }

    private static Job NewJob(string name, params JobCommand[] commands) => new()
    {
        Id = IdFor("job", name),
        ProjectId = Id,
        Name = name,
        Commands = [.. commands],
    };

    private static JobCommand Harvest(string name) => new() { Stage = CommandStage.Harvest, Name = name };

    private static JobCommand Adapt(string name) => new() { Stage = CommandStage.Adapt, Name = name };
}
