using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Core.Platform;
using Collector.Core.Staging;
using Collector.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Collector.Storing.Tests;

internal sealed class StoreScenario
{
    private StoreScenario(CollectorDatabase database, Project project)
    {
        Database = database;
        Project = project;
        Writer = new StoreWriter(database, Time);
    }

    public CollectorDatabase Database { get; }
    public Project Project { get; }
    public FixedTime Time { get; } = new();
    public StoreWriter Writer { get; }

    public static async Task<StoreScenario> CreateAsync(CollectorDatabase database)
    {
        var project = NewProject();
        await database.Projects.InsertOneAsync(project);
        return new StoreScenario(database, project);
    }

    public static Project NewProject()
    {
        return new Project
        {
            Id = Guid.NewGuid(),
            Name = $"test-{Guid.NewGuid():N}",
            Seed = Guid.NewGuid(),
        };
    }

    public Product NewProduct(string provider, string providerId, string? brand, string? sku)
    {
        return new Product
        {
            Id = ProductIdentity.Candidate(Project.Seed, provider, providerId),
            ProjectId = Project.Id,
            Namespace = $"clothing/shoes/{provider}",
            Provider = provider,
            ProviderId = providerId,
            SourceUrl = $"https://{provider}.example/p/{providerId}",
            HarvestedAt = FixedTime.Start,
            Name = $"{brand} {sku}".Trim(),
            Brand = brand,
            Sku = sku,
        };
    }

    public Listing NewListing(string provider, string providerId)
    {
        return new Listing
        {
            Id = DeterministicId.For(Project.Seed, provider, "listing", providerId),
            ProjectId = Project.Id,
            Namespace = $"clothing/shoes/{provider}",
            Provider = provider,
            ProviderId = providerId,
            SourceUrl = $"https://{provider}.example/p/{providerId}",
            HarvestedAt = FixedTime.Start,
            ProductId = ProductIdentity.Candidate(Project.Seed, provider, providerId),
            Title = $"{provider} {providerId}",
            Url = $"https://{provider}.example/p/{providerId}",
        };
    }

    public Category NewCategory(string provider, string path)
    {
        return new Category
        {
            Id = DeterministicId.For(Project.Seed, provider, "category", path),
            ProjectId = Project.Id,
            Namespace = $"clothing/shoes/{provider}",
            Provider = provider,
            ProviderId = path,
            HarvestedAt = FixedTime.Start,
            Name = path,
            Path = path,
        };
    }

    public async Task<Guid> StageAsync(
        Entity entity, EntityKind? kind = null, Guid? runId = null, Guid? projectId = null)
    {
        var document = new StagingDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId ?? Project.Id,
            RunId = runId ?? Guid.Empty,
            Kind = kind ?? KindOf(entity),
            EntityId = entity.Id,
            LakeKey = $"{Project.Id}/test/{entity.ProviderId}",
            LakeVersion = 1,
            AdaptedAt = Time.GetUtcNow().UtcDateTime,
            State = StagingState.Pending,
            Entity = entity,
        };
        await Database.Staging.InsertOneAsync(document);
        return document.Id;
    }

    public Task<ApplyResult> ApplyAsync(Guid stagingId)
    {
        return Writer.ApplyAsync(Project.Id, stagingId, CancellationToken.None);
    }

    public async Task<ApplyResult> StageAndApplyAsync(Entity entity)
    {
        var stagingId = await StageAsync(entity);
        var result = await ApplyAsync(stagingId);
        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        return result;
    }

    public async Task<List<Product>> ProductsAsync()
    {
        return await Database.Products.Find(product => product.ProjectId == Project.Id).ToListAsync();
    }

    public async Task<Product> ProductAsync(Guid id)
    {
        return await Database.Products.Find(product => product.Id == id).SingleAsync();
    }

    public async Task<Listing> ListingAsync(Guid id)
    {
        return await Database.Listings.Find(listing => listing.Id == id).SingleAsync();
    }

    public async Task<StagingDocument> StagingAsync(Guid id)
    {
        return await Database.Staging.Find(staged => staged.Id == id).SingleAsync();
    }

    public async Task<BsonDocument> RawAsync(string collection, Guid id)
    {
        var documents = Database.Database.GetCollection<BsonDocument>(collection);
        var filter = Builders<BsonDocument>.Filter.Eq("_id", Binary(id));
        return await documents.Find(filter).SingleAsync();
    }

    public async Task InsertRawAsync(string collection, BsonDocument document)
    {
        await Database.Database.GetCollection<BsonDocument>(collection).InsertOneAsync(document);
    }

    public async Task<Guid> StageUnreadableAsync(Guid runId)
    {
        var id = Guid.NewGuid();
        var document = new BsonDocument
        {
            { "_id", Binary(id) },
            { "projectId", Binary(Project.Id) },
            { "runId", Binary(runId) },
            { "kind", nameof(EntityKind.Product) },
            { "entityId", Binary(Guid.NewGuid()) },
            { "lakeKey", $"{Project.Id}/test/widget" },
            { "lakeVersion", 1 },
            { "adaptedAt", new BsonDateTime(Time.GetUtcNow().UtcDateTime) },
            { "state", nameof(StagingState.Pending) },
            { "entity", new BsonDocument("_t", "widget") },
        };
        await InsertRawAsync(CollectionNames.Staging, document);
        return id;
    }

    public static BsonBinaryData Binary(Guid id) => new(id, GuidRepresentation.Standard);

    private static EntityKind KindOf(Entity entity)
    {
        if (entity is Product) return EntityKind.Product;
        if (entity is Listing) return EntityKind.Listing;
        return EntityKind.Category;
    }
}
