using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Core.Staging;
using Collector.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Collector.Storing;

public sealed class StoreWriter
{
    private static readonly UpdateOptions Upsert = new() { IsUpsert = true };

    private readonly CollectorDatabase database;
    private readonly TimeProvider time;

    public StoreWriter(CollectorDatabase database, TimeProvider time)
    {
        MongoMapping.Register();
        this.database = database;
        this.time = time;
    }

    public async Task<ApplyResult> ApplyAsync(Guid projectId, Guid stagingId, CancellationToken cancellationToken)
    {
        var result = await InTransactionAsync(Apply, cancellationToken);
        if (result.Outcome == ApplyOutcome.Applied) await InTransactionAsync(Settle, cancellationToken);
        return result;

        Task<ApplyResult> Apply(IClientSessionHandle session, CancellationToken token)
        {
            return ApplyInTransactionAsync(session, projectId, stagingId, token);
        }

        Task<bool> Settle(IClientSessionHandle session, CancellationToken token)
        {
            var scope = new ApplyScope(session, projectId, time.GetUtcNow().UtcDateTime, token);
            return SettleAsync(scope, result);
        }
    }

    public async Task<StoreReport> ApplyPendingAsync(
        Guid projectId, Guid? runId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var filter = Builders<StagingDocument>.Filter;
        var pending = filter.Eq(staged => staged.ProjectId, projectId);
        pending &= filter.Eq(staged => staged.State, StagingState.Pending);
        if (runId is not null) pending &= filter.Eq(staged => staged.RunId, runId.Value);

        var sort = Builders<StagingDocument>.Sort.Ascending(Elements.StagedHarvestedAt);
        sort = sort.Ascending(staged => staged.AdaptedAt).Ascending(staged => staged.Id);
        var find = database.Staging.Find(pending).Sort(sort);
        var ids = await find.Limit(limit).Project(staged => staged.Id).ToListAsync(cancellationToken);

        var results = new List<ApplyResult>();
        foreach (var id in ids) results.Add(await TryApplyAsync(projectId, id, cancellationToken));

        return new StoreReport
        {
            Applied = results.Count(result => result.Outcome == ApplyOutcome.Applied),
            Rejected = results.Count(result => result.Outcome == ApplyOutcome.Rejected),
            Failed = results.Count(result => result.Outcome == ApplyOutcome.Failed),
            Results = results,
        };
    }

    private async Task<ApplyResult> TryApplyAsync(Guid projectId, Guid stagingId, CancellationToken cancellationToken)
    {
        try
        {
            return await ApplyAsync(projectId, stagingId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var reason = $"{exception.GetType().Name}: {exception.Message}";
            return new ApplyResult { StagingId = stagingId, Outcome = ApplyOutcome.Failed, Reason = reason };
        }
    }

    private async Task<T> InTransactionAsync<T>(
        Func<IClientSessionHandle, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        var client = database.Database.Client;
        using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(work, cancellationToken: cancellationToken);
    }

    private async Task<ApplyResult> ApplyInTransactionAsync(
        IClientSessionHandle session, Guid projectId, Guid stagingId, CancellationToken cancellationToken)
    {
        var raw = await FindStagingAsync(session, projectId, stagingId, cancellationToken);
        if (raw is null) return new ApplyResult { StagingId = stagingId, Outcome = ApplyOutcome.NotFound };

        var staging = StagingReader.TryRead(raw);
        var result = new ApplyResult { StagingId = stagingId, Kind = staging?.Kind, EntityId = staging?.EntityId };
        if (StagingReader.IsApplied(raw)) return result with { Outcome = ApplyOutcome.AlreadyApplied };

        var projects = database.Projects.Find(session, project => project.Id == projectId);
        var project = await projects.FirstOrDefaultAsync(cancellationToken);
        if (project is null || staging is null)
        {
            var problem = project is null ? RejectionReasons.UnknownProject : RejectionReasons.Unreadable;
            return await RejectAsync(session, result, problem, cancellationToken);
        }

        var reason = Check(staging, projectId);
        if (reason is not null) return await RejectAsync(session, result, reason, cancellationToken);

        var scope = new ApplyScope(session, projectId, time.GetUtcNow().UtcDateTime, cancellationToken);
        Guid storedId;
        if (staging.Entity is Product product)
        {
            storedId = await ApplyProductAsync(scope, project.Seed, product);
        }
        else if (staging.Entity is Listing listing)
        {
            storedId = await ApplyListingAsync(scope, listing);
        }
        else
        {
            storedId = await ApplyCategoryAsync(scope, (Category)staging.Entity);
        }

        await MarkAsync(session, stagingId, StagingState.Applied, null, cancellationToken);
        return result with { Outcome = ApplyOutcome.Applied, EntityId = storedId };
    }

    private async Task<Guid> ApplyProductAsync(ApplyScope scope, Guid seed, Product staged)
    {
        var candidate = staged.Id;
        var canonical = ProductIdentity.Canonical(seed, staged.Brand, staged.Sku);
        var resolved = canonical ?? await ResolveProductAsync(scope, candidate);
        var target = resolved ?? candidate;
        if (target != candidate) target = await NewerClaimOwnerAsync(scope, staged, target) ?? target;

        if (target != candidate) await FoldCandidateAsync(scope, candidate, target);

        await UpsertProductAsync(scope, target, staged, folding: false);

        if (target != candidate)
        {
            await TakeCandidateAsync(scope, staged, target);
            await RepointListingsAsync(scope, [candidate], target);
        }

        await RefreshStockAsync(scope, target);
        return target;
    }

    private async Task FoldCandidateAsync(ApplyScope scope, Guid candidate, Guid target)
    {
        var orphan = await FindProductAsync(scope, candidate);
        if (orphan is null) return;

        await UpsertProductAsync(scope, target, orphan, folding: true);
        var delete = ProductFilter(scope.ProjectId, candidate);
        await database.Products.DeleteOneAsync(scope.Session, delete, cancellationToken: scope.Token);
    }

    private async Task UpsertProductAsync(ApplyScope scope, Guid target, Product source, bool folding)
    {
        var stored = await FindRawAsync(scope, CollectionNames.Products, target);
        var update = MergeUpdate.From(source, scope.ProjectId, scope.Now, Elements.Brand);
        var brandMissing = !MergeUpdate.IsValued(stored, Elements.Brand);
        if (!string.IsNullOrWhiteSpace(source.Brand) && brandMissing) update.CopyStaged(Elements.Brand);
        if (update.IsOlderThan(stored)) update.KeepStoredValues(stored);

        var aliases = new List<Guid>();
        if (target != source.Id) aliases.Add(source.Id);
        if (folding)
        {
            aliases.AddRange(source.Aliases);
            update.Insert(Elements.CreatedAt, new BsonDateTime(source.CreatedAt));
        }

        update.AddToSet(Elements.Aliases, aliases);
        if (!folding && target != source.Id) update.ClaimAlias(source.Id);

        var filter = ProductFilter(scope.ProjectId, target);
        var definition = update.ToDefinition<Product>();
        await database.Products.UpdateOneAsync(scope.Session, filter, definition, Upsert, scope.Token);
    }

    private async Task<Guid> ApplyListingAsync(ApplyScope scope, Listing staged)
    {
        Guid? productId = null;
        if (staged.ProductId is { } stagedProductId)
        {
            var resolved = await ResolveProductAsync(scope, stagedProductId);
            productId = resolved ?? stagedProductId;
        }

        var raw = await FindRawAsync(scope, CollectionNames.Listings, staged.Id);
        var stored = raw is null ? null : BsonSerializer.Deserialize<Listing>(raw);

        var handled = new[] { Elements.ProductId, Elements.Sizes, Elements.Availability, Elements.OldPrice };
        var update = MergeUpdate.From(staged, scope.ProjectId, scope.Now, handled);
        if (productId is not null) update.Set(Elements.ProductId, MergeUpdate.ToBson(productId.Value));
        if (staged.Availability != Availability.Unknown) update.CopyStaged(Elements.Availability);
        if (stored is not null && update.IsOlderThan(raw)) FillListingGaps(update, staged, stored, raw);
        else OverwriteListing(update, staged);

        var filter = ListingFilter(scope.ProjectId, staged.Id);
        var definition = update.ToDefinition<Listing>();
        await database.Listings.UpdateOneAsync(scope.Session, filter, definition, Upsert, scope.Token);

        var previous = stored?.ProductId;
        if (productId is not null) await RefreshStockAsync(scope, productId.Value);
        if (previous is not null && previous != productId) await RefreshStockAsync(scope, previous.Value);

        return staged.Id;
    }

    private static void FillListingGaps(MergeUpdate update, Listing staged, Listing stored, BsonDocument raw)
    {
        var stagedStock = staged.StockCheckedAt is not null || staged.Sizes.Count > 0;
        var storedStock = stored.StockCheckedAt is not null || stored.Sizes.Count > 0;
        if (stagedStock && !storedStock) update.CopyStaged(Elements.Sizes);
        if (stored.Price is null) update.CopyStaged(Elements.OldPrice);
        update.KeepStoredValues(raw);
    }

    private static void OverwriteListing(MergeUpdate update, Listing staged)
    {
        if (staged.StockCheckedAt is not null || staged.Sizes.Count > 0) update.CopyStaged(Elements.Sizes);
        if (staged.Price is not null && staged.OldPrice is null) update.Unset(Elements.OldPrice);
        else update.CopyStaged(Elements.OldPrice);
    }

    private async Task<Guid> ApplyCategoryAsync(ApplyScope scope, Category staged)
    {
        var filter = Builders<Category>.Filter.Eq(category => category.ProjectId, scope.ProjectId);
        filter &= Builders<Category>.Filter.Eq(category => category.Id, staged.Id);
        var stored = await FindRawAsync(scope, CollectionNames.Categories, staged.Id);
        var update = MergeUpdate.From(staged, scope.ProjectId, scope.Now);
        if (update.IsOlderThan(stored)) update.KeepStoredValues(stored);

        var definition = update.ToDefinition<Category>();
        await database.Categories.UpdateOneAsync(scope.Session, filter, definition, Upsert, scope.Token);
        return staged.Id;
    }

    private async Task<Guid?> ResolveProductAsync(ApplyScope scope, Guid id)
    {
        var filter = Builders<Product>.Filter;
        var byIdOrAlias = filter.Eq(product => product.Id, id) | filter.AnyEq(product => product.Aliases, id);
        var match = filter.Eq(product => product.ProjectId, scope.ProjectId) & byIdOrAlias;
        var find = database.Products.Find(scope.Session, match).Project(product => product.Id);
        var ids = await find.ToListAsync(scope.Token);
        if (ids.Count == 0) return null;

        return ids.Contains(id) ? id : ids[0];
    }

    private async Task<Guid?> NewerClaimOwnerAsync(ApplyScope scope, Product staged, Guid target)
    {
        if (staged.HarvestedAt == default) return null;

        var claim = MergeUpdate.AliasClaim(staged.Id);
        var filter = Builders<Product>.Filter;
        var owners = filter.Eq(product => product.ProjectId, scope.ProjectId);
        owners &= filter.Ne(product => product.Id, target) & filter.AnyEq(product => product.Aliases, staged.Id);
        owners &= filter.Gt(new StringFieldDefinition<Product, DateTime>(claim), staged.HarvestedAt);
        var sort = Builders<Product>.Sort.Descending(claim);
        var find = database.Products.Find(scope.Session, owners).Sort(sort).Limit(1);
        var ids = await find.Project(product => product.Id).ToListAsync(scope.Token);
        return ids.Count == 0 ? null : ids[0];
    }

    private async Task TakeCandidateAsync(ApplyScope scope, Product candidate, Guid owner)
    {
        var filter = Builders<Product>.Filter;
        var others = filter.Eq(product => product.ProjectId, scope.ProjectId) & filter.Ne(product => product.Id, owner);
        others &= filter.AnyEq(product => product.Aliases, candidate.Id);
        var find = database.Products.Find(scope.Session, others).Project(product => product.Id);
        var previousOwners = await find.ToListAsync(scope.Token);

        foreach (var previous in previousOwners) await ReleaseCandidateAsync(scope, candidate, previous, owner);
    }

    private async Task ReleaseCandidateAsync(ApplyScope scope, Product candidate, Guid previous, Guid owner)
    {
        var pull = Builders<Product>.Update.Pull(product => product.Aliases, candidate.Id);
        pull = pull.Unset(MergeUpdate.AliasClaim(candidate.Id));
        var previousProduct = ProductFilter(scope.ProjectId, previous);
        await database.Products.UpdateOneAsync(scope.Session, previousProduct, pull, cancellationToken: scope.Token);

        var filter = Builders<Listing>.Filter;
        var sameItem = filter.Eq(listing => listing.ProjectId, scope.ProjectId);
        sameItem &= filter.Eq(listing => listing.ProductId, previous);
        sameItem &= filter.Eq(listing => listing.Provider, candidate.Provider);
        sameItem &= filter.Eq(listing => listing.ProviderId, candidate.ProviderId);
        var repoint = Builders<Listing>.Update.Set(listing => listing.ProductId, owner);
        await database.Listings.UpdateManyAsync(scope.Session, sameItem, repoint, cancellationToken: scope.Token);

        if (!await DeleteWhenAbandonedAsync(scope, previous)) await RefreshStockAsync(scope, previous);
    }

    private async Task<bool> DeleteWhenAbandonedAsync(ApplyScope scope, Guid productId)
    {
        var product = await FindProductAsync(scope, productId);
        if (product is null || product.Aliases.Count > 0) return false;

        var filter = Builders<Listing>.Filter;
        var ofProduct = filter.Eq(listing => listing.ProjectId, scope.ProjectId);
        ofProduct &= filter.Eq(listing => listing.ProductId, productId);
        if (await database.Listings.Find(scope.Session, ofProduct).AnyAsync(scope.Token)) return false;

        var delete = ProductFilter(scope.ProjectId, productId);
        await database.Products.DeleteOneAsync(scope.Session, delete, cancellationToken: scope.Token);
        return true;
    }

    private async Task RepointListingsAsync(ApplyScope scope, IEnumerable<Guid> from, Guid to)
    {
        var aliases = from.Select(id => (Guid?)id).ToList();
        var filter = Builders<Listing>.Filter;
        var pointing = filter.Eq(listing => listing.ProjectId, scope.ProjectId);
        pointing &= filter.In(listing => listing.ProductId, aliases);
        var repoint = Builders<Listing>.Update.Set(listing => listing.ProductId, to);
        await database.Listings.UpdateManyAsync(scope.Session, pointing, repoint, cancellationToken: scope.Token);
    }

    private async Task<bool> SettleAsync(ApplyScope scope, ApplyResult applied)
    {
        if (applied.EntityId is not { } id) return false;

        if (applied.Kind == EntityKind.Product) await SettleProductAsync(scope, id);
        if (applied.Kind == EntityKind.Listing) await SettleListingAsync(scope, id);
        return true;
    }

    private async Task SettleProductAsync(ApplyScope scope, Guid productId)
    {
        var product = await FindProductAsync(scope, productId);
        if (product is null) return;

        if (product.Aliases.Count > 0) await RepointListingsAsync(scope, product.Aliases, productId);
        await RefreshStockAsync(scope, productId);
    }

    private async Task SettleListingAsync(ApplyScope scope, Guid listingId)
    {
        var filter = ListingFilter(scope.ProjectId, listingId);
        var listing = await database.Listings.Find(scope.Session, filter).FirstOrDefaultAsync(scope.Token);
        if (listing?.ProductId is not { } pointedAt) return;

        var productId = await ResolveProductAsync(scope, pointedAt);
        if (productId is null) return;

        if (productId != pointedAt)
        {
            var repoint = Builders<Listing>.Update.Set(stored => stored.ProductId, productId);
            await database.Listings.UpdateOneAsync(scope.Session, filter, repoint, cancellationToken: scope.Token);
        }

        await RefreshStockAsync(scope, productId.Value);
    }

    private async Task RefreshStockAsync(ApplyScope scope, Guid productId)
    {
        var products = ProductFilter(scope.ProjectId, productId);
        var reset = Builders<Product>.Update.Unset(product => product.AnyListingInStock);
        await database.Products.UpdateOneAsync(scope.Session, products, reset, cancellationToken: scope.Token);

        var filter = Builders<Listing>.Filter;
        var ofProduct = filter.Eq(listing => listing.ProjectId, scope.ProjectId);
        ofProduct &= filter.Eq(listing => listing.ProductId, productId);
        var inStock = filter.Eq(listing => listing.Availability, Availability.InStock);
        inStock |= filter.ElemMatch(listing => listing.Sizes, size => size.InStock == true);
        var stockKnown = filter.In(listing => listing.Availability, [Availability.InStock, Availability.OutOfStock]);
        stockKnown |= filter.ElemMatch(listing => listing.Sizes, size => size.InStock != null);

        bool? flag = null;
        if (await AnyListingAsync(scope, ofProduct & inStock)) flag = true;
        else if (await AnyListingAsync(scope, ofProduct & stockKnown)) flag = false;
        if (flag is not { } known) return;

        var update = Builders<Product>.Update.Set(product => product.AnyListingInStock, known);
        await database.Products.UpdateOneAsync(scope.Session, products, update, cancellationToken: scope.Token);
    }

    private Task<bool> AnyListingAsync(ApplyScope scope, FilterDefinition<Listing> filter)
    {
        return database.Listings.Find(scope.Session, filter).AnyAsync(scope.Token);
    }

    private async Task<Product?> FindProductAsync(ApplyScope scope, Guid id)
    {
        var products = database.Products.Find(scope.Session, ProductFilter(scope.ProjectId, id));
        return await products.FirstOrDefaultAsync(scope.Token);
    }

    private async Task<BsonDocument?> FindRawAsync(ApplyScope scope, string collection, Guid id)
    {
        var documents = database.Database.GetCollection<BsonDocument>(collection);
        var filter = Builders<BsonDocument>.Filter;
        var match = filter.Eq(Elements.Id, MergeUpdate.ToBson(id));
        match &= filter.Eq(Elements.ProjectId, MergeUpdate.ToBson(scope.ProjectId));
        return await documents.Find(scope.Session, match).FirstOrDefaultAsync(scope.Token);
    }

    private async Task<BsonDocument?> FindStagingAsync(
        IClientSessionHandle session, Guid projectId, Guid stagingId, CancellationToken token)
    {
        var staging = database.Database.GetCollection<BsonDocument>(CollectionNames.Staging);
        var filter = Builders<BsonDocument>.Filter;
        var match = filter.Eq(Elements.Id, MergeUpdate.ToBson(stagingId));
        match &= filter.Eq(Elements.ProjectId, MergeUpdate.ToBson(projectId));
        return await staging.Find(session, match).FirstOrDefaultAsync(token);
    }

    private async Task<ApplyResult> RejectAsync(
        IClientSessionHandle session, ApplyResult result, string reason, CancellationToken token)
    {
        await MarkAsync(session, result.StagingId, StagingState.Rejected, reason, token);
        return result with { Outcome = ApplyOutcome.Rejected, Reason = reason };
    }

    private async Task MarkAsync(
        IClientSessionHandle session, Guid stagingId, StagingState state, string? reason, CancellationToken token)
    {
        var update = Builders<StagingDocument>.Update.Set(staged => staged.State, state);
        if (reason is null) update = update.Unset(staged => staged.Reason);
        else update = update.Set(staged => staged.Reason, reason);

        var filter = Builders<StagingDocument>.Filter.Eq(staged => staged.Id, stagingId);
        await database.Staging.UpdateOneAsync(session, filter, update, cancellationToken: token);
    }

    private static string? Check(StagingDocument staging, Guid projectId)
    {
        var entity = staging.Entity;
        if (KindOf(entity) != staging.Kind) return RejectionReasons.WrongKind;
        if (entity.ProjectId != projectId) return RejectionReasons.ProjectMismatch;

        var missing = entity.Id == Guid.Empty || string.IsNullOrWhiteSpace(entity.Provider);
        if (missing || string.IsNullOrWhiteSpace(entity.ProviderId)) return RejectionReasons.MissingIds;
        if (entity is Listing listing && listing.ProductId == Guid.Empty) return RejectionReasons.MissingIds;
        if (entity.Id != staging.EntityId) return RejectionReasons.EntityIdMismatch;

        return null;
    }

    private static EntityKind? KindOf(Entity entity)
    {
        if (entity is Product) return EntityKind.Product;
        if (entity is Listing) return EntityKind.Listing;
        if (entity is Category) return EntityKind.Category;
        return null;
    }

    private static FilterDefinition<Product> ProductFilter(Guid projectId, Guid id)
    {
        var filter = Builders<Product>.Filter;
        return filter.Eq(product => product.ProjectId, projectId) & filter.Eq(product => product.Id, id);
    }

    private static FilterDefinition<Listing> ListingFilter(Guid projectId, Guid id)
    {
        var filter = Builders<Listing>.Filter;
        return filter.Eq(listing => listing.ProjectId, projectId) & filter.Eq(listing => listing.Id, id);
    }
}
