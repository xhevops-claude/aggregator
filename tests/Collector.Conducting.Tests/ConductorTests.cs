using Collector.Core.Messages;
using Collector.Core.Runs;
using Collector.Core.Staging;

namespace Collector.Conducting.Tests;

[Trait("Category", "Integration")]
public sealed class ConductorTests(RigDatabase rig) : IClassFixture<RigDatabase>, IAsyncLifetime
{
    private const string KeyA = "p/sportm_mk/catalog-page/a";
    private const string KeyB = "p/sportm_mk/catalog-page/b";

    private ConductorScenario scenario = null!;

    public async Task InitializeAsync()
    {
        scenario = new ConductorScenario(rig.Database);
        await scenario.SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_requested_run_starts_with_its_first_harvest_command()
    {
        var request = scenario.Request("sportm.discover", limit: 1);

        var run = await scenario.Conductor.StartAsync(request, default);

        Assert.Equal(RunState.Running, run.State);
        var harvest = Assert.Single(scenario.Publisher.Of<HarvestCommand>(MessageNames.HarvestCommand));
        Assert.Equal(run.Id, harvest.RunId);
        Assert.Equal("sportm.catalog-pages", harvest.Command);
        Assert.Equal(ConductorScenario.SourceKey, harvest.Source);
        Assert.Equal("adidas", harvest.Parameters["brand"]);
        Assert.False(harvest.Parameters.ContainsKey("limit"));
        Assert.Equal(1, harvest.Limit);
        Assert.Equal(TimeSpan.FromSeconds(1), harvest.Pacing.Interval);
    }

    [Fact]
    public async Task A_redelivered_request_does_not_start_a_second_run()
    {
        var request = scenario.Request("sportm.discover");

        await scenario.Conductor.StartAsync(request, default);
        await scenario.Conductor.StartAsync(request, default);

        Assert.Single(scenario.Publisher.Of<HarvestCommand>(MessageNames.HarvestCommand));
    }

    [Fact]
    public async Task The_harvest_completion_dispatches_the_adapt_with_its_keys()
    {
        var run = await scenario.Conductor.StartAsync(scenario.Request("sportm.discover"), default);
        var harvest = scenario.Publisher.Of<HarvestCommand>(MessageNames.HarvestCommand).Single();
        var completed = scenario.Completed(harvest, RunItemState.Done, KeyA, KeyB);

        await scenario.Conductor.OnItemCompletedAsync(completed, default);

        var adapt = Assert.Single(scenario.Publisher.Of<AdaptCommand>(MessageNames.AdaptCommand));
        Assert.Equal(run.Id, adapt.RunId);
        Assert.Equal("sportm.shoes", adapt.Command);
        Assert.Equal([KeyA, KeyB], adapt.LakeKeys);
    }

    [Fact]
    public async Task A_duplicate_harvest_completion_dispatches_the_adapt_once()
    {
        await scenario.Conductor.StartAsync(scenario.Request("sportm.discover"), default);
        var harvest = scenario.Publisher.Of<HarvestCommand>(MessageNames.HarvestCommand).Single();
        var completed = scenario.Completed(harvest, RunItemState.Done, KeyA);

        await scenario.Conductor.OnItemCompletedAsync(completed, default);
        await scenario.Conductor.OnItemCompletedAsync(completed, default);

        Assert.Single(scenario.Publisher.Of<AdaptCommand>(MessageNames.AdaptCommand));
    }

    [Fact]
    public async Task The_run_is_done_only_when_the_store_has_applied_everything()
    {
        var run = await scenario.Conductor.StartAsync(scenario.Request("sportm.discover"), default);
        var harvest = scenario.Publisher.Of<HarvestCommand>(MessageNames.HarvestCommand).Single();
        await scenario.Conductor.OnItemCompletedAsync(scenario.Completed(harvest, RunItemState.Done, KeyA), default);
        var adapt = scenario.Publisher.Of<AdaptCommand>(MessageNames.AdaptCommand).Single();
        var first = await scenario.StageAsync(run.Id, StagingState.Pending);
        var second = await scenario.StageAsync(run.Id, StagingState.Pending);

        await scenario.Conductor.OnItemCompletedAsync(scenario.Completed(adapt, RunItemState.Done), default);
        Assert.Equal(RunState.Running, (await scenario.RunAsync(run.Id)).State);

        await scenario.SetStagingStateAsync(first, StagingState.Applied);
        await scenario.Conductor.OnItemCompletedAsync(StoreCompleted(run.Id, first), default);
        Assert.Equal(RunState.Running, (await scenario.RunAsync(run.Id)).State);

        await scenario.SetStagingStateAsync(second, StagingState.Applied);
        await scenario.Conductor.OnItemCompletedAsync(StoreCompleted(run.Id, second), default);
        var finished = await scenario.RunAsync(run.Id);
        Assert.Equal(RunState.Done, finished.State);
        Assert.Equal(ConductorScenario.Start, finished.FinishedAt);
        Assert.Single(await scenario.EventsAsync(run.Id), e => e.Type == "finished");
    }

    [Fact]
    public async Task A_harvest_with_no_keys_skips_the_adapt_and_finishes()
    {
        var run = await scenario.Conductor.StartAsync(scenario.Request("sportm.discover"), default);
        var harvest = scenario.Publisher.Of<HarvestCommand>(MessageNames.HarvestCommand).Single();

        await scenario.Conductor.OnItemCompletedAsync(scenario.Completed(harvest, RunItemState.Done), default);

        Assert.Empty(scenario.Publisher.Of<AdaptCommand>(MessageNames.AdaptCommand));
        Assert.Equal(RunState.Done, (await scenario.RunAsync(run.Id)).State);
    }

    [Fact]
    public async Task A_failed_harvest_without_keys_waits_for_a_decision()
    {
        var run = await scenario.Conductor.StartAsync(scenario.Request("sportm.discover"), default);
        var harvest = scenario.Publisher.Of<HarvestCommand>(MessageNames.HarvestCommand).Single();

        await scenario.Conductor.OnItemCompletedAsync(scenario.Completed(harvest, RunItemState.Failed), default);

        Assert.Equal(RunState.WaitingForDecision, (await scenario.RunAsync(run.Id)).State);
    }

    [Fact]
    public async Task A_rejected_staging_document_makes_the_run_wait_for_a_decision()
    {
        var run = await scenario.Conductor.StartAsync(scenario.Request("sportm.discover"), default);
        var harvest = scenario.Publisher.Of<HarvestCommand>(MessageNames.HarvestCommand).Single();
        await scenario.Conductor.OnItemCompletedAsync(scenario.Completed(harvest, RunItemState.Done, KeyA), default);
        var adapt = scenario.Publisher.Of<AdaptCommand>(MessageNames.AdaptCommand).Single();
        var staged = await scenario.StageAsync(run.Id, StagingState.Rejected);

        await scenario.Conductor.OnItemCompletedAsync(scenario.Completed(adapt, RunItemState.Done), default);
        await scenario.Conductor.OnItemCompletedAsync(StoreCompleted(run.Id, staged), default);

        Assert.Equal(RunState.WaitingForDecision, (await scenario.RunAsync(run.Id)).State);
    }

    [Fact]
    public async Task The_details_job_targets_listings_without_details()
    {
        await scenario.AddListingAsync("B2", detailed: false);
        await scenario.AddListingAsync("A1", detailed: false);
        await scenario.AddListingAsync("C3", detailed: true);
        await scenario.AddListingAsync("D4", detailed: false);

        await scenario.Conductor.StartAsync(scenario.Request("sportm.details", limit: 2), default);

        var harvest = Assert.Single(scenario.Publisher.Of<HarvestCommand>(MessageNames.HarvestCommand));
        Assert.Equal("sportm.product-page", harvest.Command);
        Assert.Equal(["A1", "B2"], harvest.Targets.Select(target => target.ProviderId));
        Assert.All(harvest.Targets, target => Assert.Contains(target.ProviderId, target.Url));
    }

    [Fact]
    public async Task A_details_run_with_nothing_to_fetch_finishes_without_harvesting()
    {
        await scenario.AddListingAsync("C3", detailed: true);

        var run = await scenario.Conductor.StartAsync(scenario.Request("sportm.details"), default);

        Assert.Empty(scenario.Publisher.Published);
        Assert.Equal(RunState.Done, (await scenario.RunAsync(run.Id)).State);
    }

    [Fact]
    public async Task A_job_not_paired_with_the_leaf_is_refused()
    {
        var request = scenario.Request("sportm.unpaired");

        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.Conductor.StartAsync(request, default));
        Assert.Empty(scenario.Publisher.Published);
    }

    [Fact]
    public async Task An_unknown_job_is_refused()
    {
        var request = scenario.Request("sportm.nope");

        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.Conductor.StartAsync(request, default));
    }

    private ItemCompleted StoreCompleted(Guid runId, Guid stagingId)
    {
        return new ItemCompleted
        {
            ProjectId = scenario.ProjectId,
            RunId = runId,
            ItemId = stagingId,
            State = RunItemState.Done,
        };
    }
}
