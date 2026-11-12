using Collector.Core.Entities;
using Collector.Core.Messages;
using Collector.Core.Platform;
using Collector.Core.Runs;
using Collector.Core.Staging;
using Collector.Data;
using MongoDB.Driver;

namespace Collector.Conducting.Tests;

internal sealed class ConductorScenario
{
    public const string Leaf = "clothing/shoes/sportm";
    public const string SourceKey = "sportm_mk";

    public static readonly DateTime Start = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

    private readonly CollectorDatabase db;

    public ConductorScenario(CollectorDatabase db)
    {
        this.db = db;
        Conductor = new Conductor(db, Publisher, new FixedClock(Start));
    }

    public Guid ProjectId { get; } = Guid.NewGuid();
    public RecordingPublisher Publisher { get; } = new();
    public Conductor Conductor { get; }

    public Job Discover { get; private set; } = new();
    public Job Details { get; private set; } = new();
    public Job Unpaired { get; private set; } = new();

    public async Task SeedAsync()
    {
        var project = new Project { Id = ProjectId, Name = $"test-{ProjectId:N}", Seed = ProjectId };
        await db.Projects.InsertOneAsync(project);

        var source = new Source { Id = Guid.NewGuid(), ProjectId = ProjectId, Key = SourceKey, Platform = "sportm" };
        await db.Sources.InsertOneAsync(source);

        Discover = NewJob("sportm.discover", "sportm.catalog-pages");
        Details = NewJob("sportm.details", "sportm.product-page");
        Unpaired = NewJob("sportm.unpaired", "sportm.catalog-pages");
        await db.Jobs.InsertManyAsync([Discover, Details, Unpaired]);

        var pacing = new Pacing { Mode = PacingMode.Interval, Interval = TimeSpan.FromSeconds(1) };
        var strategy = new Strategy { Id = Guid.NewGuid(), ProjectId = ProjectId, Name = "dev", Pacing = pacing };
        await db.Strategies.InsertOneAsync(strategy);

        var shoes = new Operation { Id = Guid.NewGuid(), ProjectId = ProjectId, Path = "clothing/shoes" };
        shoes = shoes with { Domain = "shoes" };
        var paired = new[] { Discover, Details };
        var pairs = paired.Select(job => new OperationJob { JobId = job.Id, StrategyId = strategy.Id });
        var leaf = new Operation
        {
            Id = Guid.NewGuid(),
            ProjectId = ProjectId,
            ParentId = shoes.Id,
            Path = Leaf,
            SourceId = source.Id,
            Jobs = [.. pairs],
        };
        await db.Operations.InsertManyAsync([shoes, leaf]);
    }

    public RunRequested Request(string job, int? limit = null)
    {
        var parameters = new Dictionary<string, string> { ["brand"] = "adidas" };
        return new RunRequested
        {
            ProjectId = ProjectId,
            RunId = Guid.NewGuid(),
            Namespace = Leaf,
            Job = job,
            Parameters = parameters,
            Limit = limit,
            RequestedBy = "test",
        };
    }

    public ItemCompleted Completed(IMessage message, RunItemState state, params string[] keys)
    {
        return new ItemCompleted
        {
            ProjectId = message.ProjectId,
            RunId = message.RunId,
            ItemId = message.ItemId,
            State = state,
            LakeKeys = [.. keys],
        };
    }

    public async Task<Guid> StageAsync(Guid runId, StagingState state)
    {
        var listing = new Listing { Id = Guid.NewGuid(), ProjectId = ProjectId, Namespace = Leaf, ProviderId = "X1" };
        var staged = new StagingDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = ProjectId,
            RunId = runId,
            Kind = EntityKind.Listing,
            EntityId = listing.Id,
            State = state,
            Entity = listing,
        };
        await db.Staging.InsertOneAsync(staged);
        return staged.Id;
    }

    public Task SetStagingStateAsync(Guid stagingId, StagingState state)
    {
        var update = Builders<StagingDocument>.Update.Set(s => s.State, state);
        return db.Staging.UpdateOneAsync(s => s.Id == stagingId, update);
    }

    public async Task AddListingAsync(string providerId, bool detailed)
    {
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            ProjectId = ProjectId,
            Namespace = Leaf,
            ProviderId = providerId,
            Url = $"https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID={providerId}",
            InfoCheckedAt = detailed ? Start : null,
        };
        await db.Listings.InsertOneAsync(listing);
    }

    public Task<Run> RunAsync(Guid runId) => db.Runs.Find(r => r.Id == runId).FirstAsync();

    public Task<List<RunEvent>> EventsAsync(Guid runId) => db.RunEvents.Find(e => e.RunId == runId).ToListAsync();

    private Job NewJob(string name, string harvest)
    {
        JobCommand[] commands =
        [
            new() { Stage = CommandStage.Harvest, Name = harvest },
            new() { Stage = CommandStage.Adapt, Name = "sportm.shoes" },
        ];
        return new Job { Id = Guid.NewGuid(), ProjectId = ProjectId, Name = name, Commands = [.. commands] };
    }
}
