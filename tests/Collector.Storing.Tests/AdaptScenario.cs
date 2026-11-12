using Collector.Adapting;
using Collector.Core.Domains;
using Collector.Core.Messages;
using Collector.Core.Platform;
using Collector.Core.Staging;
using Collector.Data;
using Collector.Lake;
using MongoDB.Driver;

namespace Collector.Storing.Tests;

internal sealed class AdaptScenario
{
    public const string SourceKey = "sportm_mk";
    public const string ListingPage = "listing-page";

    private AdaptScenario(CollectorDatabase database, Project project)
    {
        Database = database;
        Project = project;
        Source = new Source
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            ProviderId = Guid.NewGuid(),
            Key = SourceKey,
            Provider = "sportm",
            BaseUrl = "https://sportm.example",
            Platform = "sportm",
            Currency = "MKD",
        };
        Operation = new Operation
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Path = "clothing/shoes/sportm",
            SourceId = Source.Id,
        };
        Runner = new AdaptRunner(Lake, database, Publisher, Time);
    }

    public CollectorDatabase Database { get; }
    public Project Project { get; }
    public Source Source { get; }
    public Operation Operation { get; }
    public MemoryLake Lake { get; } = new();
    public RecordingPublisher Publisher { get; } = new();
    public FixedTime Time { get; } = new();
    public AdaptRunner Runner { get; }

    public static async Task<AdaptScenario> CreateAsync(CollectorDatabase database)
    {
        var project = StoreScenario.NewProject();
        await database.Projects.InsertOneAsync(project);
        return new AdaptScenario(database, project);
    }

    public RawItem AddRaw(string providerId, string body, string kind = ListingPage, int version = 1)
    {
        return Lake.Add(Project.Id, SourceKey, kind, providerId, body, version);
    }

    public AdaptCommand Command(Guid runId, params string[] lakeKeys)
    {
        return new AdaptCommand
        {
            ProjectId = Project.Id,
            RunId = runId,
            ItemId = Guid.NewGuid(),
            Namespace = Operation.Path,
            Command = "sportm.listings",
            LakeKeys = [.. lakeKeys],
        };
    }

    public AdaptInput Input(AdaptCommand command, IAdapter adapter)
    {
        return new AdaptInput
        {
            Command = command,
            Operation = Operation,
            Domain = DomainNames.Shoes,
            Source = Source,
            Project = Project,
            Adapter = adapter,
        };
    }

    public Task<AdaptReport> RunAsync(AdaptCommand command, IAdapter adapter)
    {
        return Runner.RunAsync(Input(command, adapter), CancellationToken.None);
    }

    public async Task<List<StagingDocument>> StagedAsync(Guid runId)
    {
        var staged = Database.Staging.Find(document => document.ProjectId == Project.Id && document.RunId == runId);
        return await staged.ToListAsync();
    }
}
