using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Core.Messages;
using Collector.Core.Runs;
using Collector.Core.Staging;
using MongoDB.Driver;

namespace Collector.Storing.Tests;

[Trait("Category", "Integration")]
public class AdaptRunnerTests(RigDatabase rig) : IClassFixture<RigDatabase>
{
    [Fact]
    public async Task Stages_one_pending_document_per_entity_and_signals_each_one()
    {
        var scenario = await AdaptScenario.CreateAsync(rig.Database);
        var first = scenario.AddRaw("JQ5211", "adidas|HP6005");
        var second = scenario.AddRaw("JQ5212", "nike|DD1391");
        var command = scenario.Command(Guid.NewGuid(), first.Item.Key, second.Item.Key);

        var report = await scenario.RunAsync(command, ShoeLines.Adapter());

        Assert.Equal(RunItemState.Done, report.State);
        Assert.Equal(2, report.RawItems);
        Assert.Equal(4, report.Staged);
        var staged = await scenario.StagedAsync(command.RunId);
        Assert.Equal(4, staged.Count);
        Assert.All(staged, document => Assert.Equal(StagingState.Pending, document.State));
        Assert.All(staged, document => Assert.Equal(FixedTime.Start, document.AdaptedAt));
        Assert.Equal(2, staged.Count(document => document.Kind == EntityKind.Product));
        var listings = staged.Where(document => document.Kind == EntityKind.Listing);
        var listing = listings.Single(document => document.LakeKey == first.Item.Key);
        Assert.Equal(1, listing.LakeVersion);
        Assert.Equal(listing.Entity.Id, listing.EntityId);
        Assert.IsType<Listing>(listing.Entity);

        var adapted = scenario.Publisher.Of<EntityAdapted>(MessageNames.EntityAdapted);
        var signalled = adapted.Select(message => message.StagingId).Order();
        Assert.Equal(staged.Select(document => document.Id).Order(), signalled);
        Assert.All(adapted, message => Assert.Equal(command.ItemId, message.ItemId));
        var (name, message) = scenario.Publisher.Published[^1];
        Assert.Equal(MessageNames.ItemCompleted, name);
        Assert.Equal(RunItemState.Done, Assert.IsType<ItemCompleted>(message).State);
    }

    [Fact]
    public async Task Running_a_command_again_keeps_the_same_staging_documents()
    {
        var scenario = await AdaptScenario.CreateAsync(rig.Database);
        var raw = scenario.AddRaw("JQ5211", "adidas|HP6005");
        var command = scenario.Command(Guid.NewGuid(), raw.Item.Key);
        var writer = new StoreWriter(rig.Database, scenario.Time);

        var first = await scenario.RunAsync(command, ShoeLines.Adapter());
        await writer.ApplyPendingAsync(scenario.Project.Id, command.RunId, 10, CancellationToken.None);
        var second = await scenario.RunAsync(command with { ItemId = Guid.NewGuid() }, ShoeLines.Adapter());

        Assert.Equal(first.StagingIds.Order(), second.StagingIds.Order());
        var staged = await scenario.StagedAsync(command.RunId);
        Assert.Equal(2, staged.Count);
        Assert.All(staged, document => Assert.Equal(StagingState.Pending, document.State));
    }

    [Fact]
    public async Task Apples_guard_drops_entities_outside_the_operation()
    {
        var scenario = await AdaptScenario.CreateAsync(rig.Database);
        var raw = scenario.AddRaw("JQ5211", "adidas|HP6005");
        var command = scenario.Command(Guid.NewGuid(), raw.Item.Key);
        var adapter = new FakeAdapter((item, context) => ShoeLines.Adapt(item, context).SelectMany(Strays));

        var report = await scenario.RunAsync(command, adapter);

        Assert.Equal(RunItemState.Done, report.State);
        Assert.Equal(2, report.Staged);
        Assert.Equal(6, report.Rejected);
        var staged = await scenario.StagedAsync(command.RunId);
        Assert.All(staged, document => Assert.Equal(scenario.Operation.Path, document.Entity.Namespace));

        static IEnumerable<Entity> Strays(Entity entity)
        {
            yield return entity;
            yield return entity with { Namespace = "clothing/shirts/sportm" };
            yield return entity with { ProjectId = Guid.NewGuid() };
            yield return new StrayEntity { ProjectId = entity.ProjectId, Namespace = entity.Namespace };
        }
    }

    [Fact]
    public async Task Adapter_failure_fails_only_its_raw_item()
    {
        var scenario = await AdaptScenario.CreateAsync(rig.Database);
        var good = scenario.AddRaw("JQ5211", "adidas|HP6005");
        var bad = scenario.AddRaw("JQ5212", "boom|X");
        var other = scenario.AddRaw("JQ5213", "nike|DD1391");
        var command = scenario.Command(Guid.NewGuid(), good.Item.Key, bad.Item.Key, other.Item.Key);

        var report = await scenario.RunAsync(command, ShoeLines.Adapter());

        Assert.Equal(RunItemState.Failed, report.State);
        Assert.Equal(3, report.RawItems);
        Assert.Equal(4, report.Staged);
        var failure = Assert.Single(report.Failures);
        Assert.Equal(bad.Item.Key, failure.LakeKey);
        Assert.Equal(nameof(FormatException), failure.ErrorClass);
        var staged = await scenario.StagedAsync(command.RunId);
        Assert.DoesNotContain(staged, document => document.LakeKey == bad.Item.Key);
        var completed = Assert.Single(scenario.Publisher.Of<ItemCompleted>(MessageNames.ItemCompleted));
        Assert.Equal(RunItemState.Failed, completed.State);
        Assert.Equal(nameof(FormatException), completed.ErrorClass);
        Assert.Contains(bad.Item.Key, completed.ErrorText);
    }

    [Fact]
    public async Task Missing_lake_key_fails_only_that_key()
    {
        var scenario = await AdaptScenario.CreateAsync(rig.Database);
        var raw = scenario.AddRaw("JQ5211", "adidas|HP6005");
        var missing = $"{scenario.Project.Id}/{AdaptScenario.SourceKey}/{AdaptScenario.ListingPage}/NOPE";

        var command = scenario.Command(Guid.NewGuid(), raw.Item.Key, missing);

        var report = await scenario.RunAsync(command, ShoeLines.Adapter());

        Assert.Equal(2, report.Staged);
        Assert.Equal(missing, Assert.Single(report.Failures).LakeKey);
    }

    [Fact]
    public async Task Without_lake_keys_reads_the_latest_items_of_its_source_and_kinds()
    {
        var scenario = await AdaptScenario.CreateAsync(rig.Database);
        scenario.AddRaw("JQ5211", "adidas|HP6005", version: 1);
        scenario.AddRaw("JQ5211", "adidas|HP6006", version: 2);
        scenario.AddRaw("sitemap", "nike|DD1391", kind: "sitemap");
        scenario.Lake.Add(scenario.Project.Id, "sportvision_mk", AdaptScenario.ListingPage, "1", "nike|DD1391");
        var command = scenario.Command(Guid.NewGuid());

        var report = await scenario.RunAsync(command, ShoeLines.Adapter());

        Assert.Equal(1, report.RawItems);
        var staged = await scenario.StagedAsync(command.RunId);
        Assert.All(staged, document => Assert.Equal(2, document.LakeVersion));
        var product = Assert.IsType<Product>(staged.Single(document => document.Kind == EntityKind.Product).Entity);
        Assert.Equal("HP6006", product.Sku);
        Assert.Equal(AdaptScenario.SourceKey, scenario.Lake.LastQuery?.Source);
        Assert.Equal([AdaptScenario.ListingPage], scenario.Lake.LastQuery?.Kinds);
    }

    [Fact]
    public async Task Named_lake_item_of_a_kind_the_adapter_does_not_read_is_skipped()
    {
        var scenario = await AdaptScenario.CreateAsync(rig.Database);
        var sitemap = scenario.AddRaw("sitemap", "adidas|HP6005", kind: "sitemap");

        var report = await scenario.RunAsync(scenario.Command(Guid.NewGuid(), sitemap.Item.Key), ShoeLines.Adapter());

        Assert.Equal(RunItemState.Done, report.State);
        Assert.Equal(0, report.RawItems);
        Assert.Equal([sitemap.Item.Key], report.Skipped);
    }

    [Fact]
    public async Task Adapted_entities_reach_the_store_through_the_store_writer()
    {
        var scenario = await AdaptScenario.CreateAsync(rig.Database);
        var first = scenario.AddRaw("JQ5211", "adidas|HP6005");
        var second = scenario.AddRaw("JQ5212", "Adidas|hp6005");
        var command = scenario.Command(Guid.NewGuid(), first.Item.Key, second.Item.Key);
        var writer = new StoreWriter(rig.Database, scenario.Time);

        await scenario.RunAsync(command, ShoeLines.Adapter());
        var applied = await writer.ApplyPendingAsync(scenario.Project.Id, command.RunId, 10, CancellationToken.None);

        Assert.Equal(4, applied.Applied);
        var canonical = ProductIdentity.Canonical(scenario.Project.Seed, "adidas", "HP6005");
        var products = await rig.Database.Products.FindAsync(product => product.ProjectId == scenario.Project.Id);
        var product = Assert.Single(await products.ToListAsync());
        Assert.Equal(canonical, product.Id);
        Assert.True(product.AnyListingInStock);
    }

    [Fact]
    public async Task Input_that_does_not_belong_together_is_refused_before_anything_runs()
    {
        var scenario = await AdaptScenario.CreateAsync(rig.Database);
        var raw = scenario.AddRaw("JQ5211", "adidas|HP6005");
        var electronics = new FakeAdapter(ShoeLines.Adapt) { Domain = "electronics" };

        var run = scenario.RunAsync(scenario.Command(Guid.NewGuid(), raw.Item.Key), electronics);

        await Assert.ThrowsAsync<ArgumentException>(() => run);
        Assert.Empty(scenario.Publisher.Published);
    }
}
