using Collector.Core.Domains;
using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Core.Staging;
using Collector.Data;
using MongoDB.Bson;

namespace Collector.Storing.Tests;

[Trait("Category", "Integration")]
public class StoreWriterStagingTests(RigDatabase rig) : IClassFixture<RigDatabase>
{
    [Fact]
    public async Task Unknown_project_is_rejected()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var stranger = StoreScenario.NewProject();
        var product = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005") with { ProjectId = stranger.Id };
        var stagingId = await store.StageAsync(product, projectId: stranger.Id);

        var result = await store.Writer.ApplyAsync(stranger.Id, stagingId, CancellationToken.None);

        await AssertRejectedAsync(store, result, RejectionReasons.UnknownProject);
    }

    [Fact]
    public async Task Missing_ids_are_rejected()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var listing = store.NewListing("sportm", "JQ5211") with { ProviderId = "" };

        var result = await store.ApplyAsync(await store.StageAsync(listing));

        await AssertRejectedAsync(store, result, RejectionReasons.MissingIds);
        Assert.Empty(await store.ProductsAsync());
    }

    [Fact]
    public async Task Listing_with_an_empty_product_id_is_rejected()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var listing = store.NewListing("sportm", "JQ5211") with { ProductId = Guid.Empty };

        var result = await store.ApplyAsync(await store.StageAsync(listing));

        await AssertRejectedAsync(store, result, RejectionReasons.MissingIds);
    }

    [Fact]
    public async Task Entity_of_a_kind_other_than_staged_is_rejected()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var product = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005");

        var result = await store.ApplyAsync(await store.StageAsync(product, EntityKind.Listing));

        await AssertRejectedAsync(store, result, RejectionReasons.WrongKind);
        Assert.Empty(await store.ProductsAsync());
    }

    [Fact]
    public async Task Entity_of_another_project_is_rejected()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var product = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005") with { ProjectId = Guid.NewGuid() };

        var result = await store.ApplyAsync(await store.StageAsync(product));

        await AssertRejectedAsync(store, result, RejectionReasons.ProjectMismatch);
    }

    [Fact]
    public async Task Unknown_staging_id_is_not_found()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);

        var result = await store.ApplyAsync(Guid.NewGuid());

        Assert.Equal(ApplyOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Applied_document_is_marked_and_reapplying_it_is_a_no_op()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var stagingId = await store.StageAsync(store.NewProduct("sportm", "JQ5211", "adidas", "HP6005"));

        var first = await store.ApplyAsync(stagingId);
        var applied = await store.StagingAsync(stagingId);
        store.Time.Advance(TimeSpan.FromHours(1));
        var second = await store.ApplyAsync(stagingId);

        Assert.Equal(ApplyOutcome.Applied, first.Outcome);
        Assert.Equal(StagingState.Applied, applied.State);
        Assert.Null(applied.Reason);
        Assert.Equal(ApplyOutcome.AlreadyApplied, second.Outcome);
        var product = Assert.Single(await store.ProductsAsync());
        Assert.Equal(FixedTime.Start, product.UpdatedAt);
    }

    [Fact]
    public async Task Rejected_document_is_applied_once_its_problem_is_gone()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var stranger = StoreScenario.NewProject();
        var product = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005") with { ProjectId = stranger.Id };
        var stagingId = await store.StageAsync(product, projectId: stranger.Id);

        var rejected = await store.Writer.ApplyAsync(stranger.Id, stagingId, CancellationToken.None);
        await rig.Database.Projects.InsertOneAsync(stranger);
        var applied = await store.Writer.ApplyAsync(stranger.Id, stagingId, CancellationToken.None);

        Assert.Equal(ApplyOutcome.Rejected, rejected.Outcome);
        Assert.Equal(ApplyOutcome.Applied, applied.Outcome);
        Assert.Null((await store.StagingAsync(stagingId)).Reason);
    }

    [Fact]
    public async Task Apply_pending_takes_only_the_run_asked_for_up_to_the_limit()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        await store.StageAsync(store.NewProduct("sportm", "1", "adidas", "A1"), runId: runA);
        store.Time.Advance(TimeSpan.FromMinutes(1));
        await store.StageAsync(store.NewListing("sportm", "1"), runId: runA);
        store.Time.Advance(TimeSpan.FromMinutes(1));
        await store.StageAsync(store.NewListing("sportm", "2") with { ProviderId = "" }, runId: runA);
        var otherRun = await store.StageAsync(store.NewProduct("sportm", "3", "adidas", "A3"), runId: runB);

        var firstBatch = await store.Writer.ApplyPendingAsync(store.Project.Id, runA, 2, CancellationToken.None);
        var secondBatch = await store.Writer.ApplyPendingAsync(store.Project.Id, runA, 10, CancellationToken.None);

        Assert.Equal(2, firstBatch.Results.Count);
        Assert.Equal(2, firstBatch.Applied);
        Assert.Single(secondBatch.Results);
        Assert.Equal(1, secondBatch.Rejected);
        Assert.Equal(StagingState.Pending, (await store.StagingAsync(otherRun)).State);
    }

    [Fact]
    public async Task Unreadable_document_is_rejected_and_the_queue_moves_on()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var runId = Guid.NewGuid();
        var unreadable = await store.StageUnreadableAsync(runId);
        store.Time.Advance(TimeSpan.FromMinutes(1));
        var valid = await store.StageAsync(store.NewProduct("sportm", "JQ5211", "adidas", "HP6005"), runId: runId);

        var report = await store.Writer.ApplyPendingAsync(store.Project.Id, runId, 10, CancellationToken.None);
        var again = await store.ApplyAsync(unreadable);

        Assert.Equal(1, report.Applied);
        Assert.Equal(1, report.Rejected);
        var rejected = report.Results.Single(result => result.StagingId == unreadable);
        Assert.Equal(RejectionReasons.Unreadable, rejected.Reason);
        var raw = await store.RawAsync(CollectionNames.Staging, unreadable);
        Assert.Equal(nameof(StagingState.Rejected), raw["state"].AsString);
        Assert.Equal(RejectionReasons.Unreadable, raw["reason"].AsString);
        Assert.Equal(StagingState.Applied, (await store.StagingAsync(valid)).State);
        Assert.Equal(ApplyOutcome.Rejected, again.Outcome);
    }

    [Fact]
    public async Task Document_the_store_cannot_take_fails_alone_and_the_queue_moves_on()
    {
        var store = await StoreScenario.CreateAsync(rig.Database);
        var runId = Guid.NewGuid();
        var canonical = ProductIdentity.Canonical(store.Project.Seed, "adidas", "HP6005")!.Value;
        var broken = new BsonDocument
        {
            { "_id", StoreScenario.Binary(canonical) },
            { "projectId", StoreScenario.Binary(store.Project.Id) },
            { "fields", "not a document" },
        };
        await store.InsertRawAsync(CollectionNames.Products, broken);
        var product = store.NewProduct("sportm", "JQ5211", "adidas", "HP6005");
        var white = product with { Fields = new ShoeFields { Colourway = "white" } };
        var failing = await store.StageAsync(white, runId: runId);
        store.Time.Advance(TimeSpan.FromMinutes(1));
        var valid = await store.StageAsync(store.NewProduct("sportm", "JQ5212", "nike", "DD1391"), runId: runId);

        var report = await store.Writer.ApplyPendingAsync(store.Project.Id, runId, 10, CancellationToken.None);

        Assert.Equal(1, report.Applied);
        Assert.Equal(1, report.Failed);
        var failed = report.Results.Single(result => result.StagingId == failing);
        Assert.Equal(ApplyOutcome.Failed, failed.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(failed.Reason));
        Assert.Equal(StagingState.Pending, (await store.StagingAsync(failing)).State);
        Assert.Equal(StagingState.Applied, (await store.StagingAsync(valid)).State);
    }

    private static async Task AssertRejectedAsync(StoreScenario store, ApplyResult result, string reason)
    {
        Assert.Equal(ApplyOutcome.Rejected, result.Outcome);
        Assert.Equal(reason, result.Reason);
        var staged = await store.StagingAsync(result.StagingId);
        Assert.Equal(StagingState.Rejected, staged.State);
        Assert.Equal(reason, staged.Reason);
    }
}
