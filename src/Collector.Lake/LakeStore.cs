using System.Diagnostics.CodeAnalysis;
using Collector.Core.Ids;
using Collector.Core.Lake;
using Collector.Core.Platform;
using Collector.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Collector.Lake;

public sealed class LakeStore : ILakeWriter, ILakeReader
{
    private const int MaxWriteAttempts = 1000;
    private const int MaxReadAttempts = 5;

    private static readonly TimeSpan IndexPollDelay = TimeSpan.FromMilliseconds(10);

    private static readonly BsonDocument LatestPerKey =
        BsonDocument.Parse("{ $group: { _id: '$key', item: { $first: '$$ROOT' } } }");

    private static readonly BsonDocument UnwrapItem = BsonDocument.Parse("{ $replaceRoot: { newRoot: '$item' } }");

    private static readonly SortDefinition<LakeItem> NewestFirst = Builders<LakeItem>.Sort.Descending(i => i.Version);
    private static readonly SortDefinition<LakeItem> ByKey = Builders<LakeItem>.Sort.Ascending(i => i.Key);
    private static readonly SortDefinition<LakeItem> ByKeyNewestFirst = ByKey.Descending(i => i.Version);

    private readonly IMongoCollection<LakeItem> items;
    private readonly int indexPolls;

    public LakeStore(CollectorDatabase database, LakeOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Root);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.UnindexedBodyWait, TimeSpan.Zero);

        MongoMapping.Register();
        items = database.LakeItems;
        Root = Path.GetFullPath(options.Root);
        var polls = Math.Ceiling(options.UnindexedBodyWait / IndexPollDelay);
        indexPolls = (int)Math.Min(polls, int.MaxValue);
    }

    public string Root { get; }

    public string PathOf(LakeItem item) => Path.GetFullPath(Path.Combine(Root, item.Location));

    public async Task<LakeWriteResult> WriteAsync(
        LakeWrite write, LakeRetention retention, CancellationToken cancellationToken)
    {
        Validate(write);
        var token = cancellationToken;
        var lakeKey = new LakeKey(write.ProjectId, write.Source, write.Kind, write.ProviderId);
        var key = lakeKey.ToString();
        var hash = LakeBody.Hash(write.Body);

        var latest = await FindLatestAsync(write.ProjectId, key, token);
        if (IsUnchanged(latest, hash, retention)) return await TouchAsync(latest, write.HarvestedAt, token);

        var folder = Path.GetFullPath(Path.Combine(Root, LakePath.Folder(lakeKey)));
        using var body = new PendingBody(folder, LakeBody.Compress(write.Body));
        var version = (latest?.Version ?? 0) + 1;
        for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
        {
            await body.PrepareAsync(token);
            if (retention == LakeRetention.LatestOnly)
            {
                version = await UnusedVersionAsync(write.ProjectId, key, version, token);
            }

            var item = NewItem(write, lakeKey, version, hash);
            var placed = await body.TryPlaceAsync(PathOf(item), token);
            if (placed && await TryInsertAsync(item, token)) return await FinishAsync(item, retention, token);

            LakeItem? seen;
            if (placed) seen = await LostIndexSlotAsync(item, token);
            else seen = await LostBodySlotAsync(item, retention, token);

            if (IsUnchanged(seen, hash, retention)) return await TouchAsync(seen, write.HarvestedAt, token);
            version = Math.Max(version, seen?.Version ?? 0) + 1;
        }

        throw new InvalidOperationException($"No free lake version for {key} after {MaxWriteAttempts} attempts");
    }

    public async Task<RawItem?> ReadLatestAsync(Guid projectId, string key, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var item = await FindLatestAsync(projectId, key, cancellationToken);
            if (item is null) return null;

            var body = await LakeBody.TryReadAsync(PathOf(item), cancellationToken);
            if (body is not null && Matches(item, body)) return new RawItem { Item = item, Body = body };
            if (attempt < MaxReadAttempts) continue;

            if (body is null) throw MissingBody(item);
            return Verified(item, body);
        }
    }

    public async Task<RawItem?> ReadAsync(Guid projectId, string key, int version, CancellationToken cancellationToken)
    {
        var filter = KeyFilter(projectId, key) & Builders<LakeItem>.Filter.Eq(i => i.Version, version);
        var item = await items.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (item is null) return null;
        return await ReadAsync(item, cancellationToken);
    }

    public async Task<RawItem> ReadAsync(LakeItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var body = await LakeBody.TryReadAsync(PathOf(item), cancellationToken);
        if (body is null) throw MissingBody(item);
        return Verified(item, body);
    }

    public async Task<IReadOnlyList<LakeItem>> LatestAsync(LakeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var options = new AggregateOptions { AllowDiskUse = true };
        var pipeline = items.Aggregate(options).Match(QueryFilter(query)).Sort(ByKeyNewestFirst);
        pipeline = pipeline.AppendStage<LakeItem>(LatestPerKey).AppendStage<LakeItem>(UnwrapItem);
        if (query.HarvestedSince is { } since)
        {
            var storedSince = ToStoredTime(since);
            pipeline = pipeline.Match(i => i.HarvestedAt >= storedSince);
        }

        pipeline = pipeline.Sort(ByKey);
        if (query.Limit is int limit and > 0) pipeline = pipeline.Limit(limit);
        return await pipeline.ToListAsync(cancellationToken);
    }

    private async Task<bool> TryInsertAsync(LakeItem item, CancellationToken token)
    {
        try
        {
            await items.InsertOneAsync(item, cancellationToken: token);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    private async Task<LakeItem?> LostIndexSlotAsync(LakeItem item, CancellationToken token)
    {
        LakeBody.TryDelete(PathOf(item));
        return await FindLatestAsync(item.ProjectId, item.Key, token);
    }

    private async Task<LakeItem?> LostBodySlotAsync(LakeItem item, LakeRetention retention, CancellationToken token)
    {
        if (retention != LakeRetention.ChangesOnly) return null;
        return await AwaitIndexedAsync(item, token);
    }

    private async Task<int> UnusedVersionAsync(Guid projectId, string key, int version, CancellationToken token)
    {
        var latest = await FindLatestAsync(projectId, key, token);
        return Math.Max(version, (latest?.Version ?? 0) + 1);
    }

    private async Task<LakeItem?> AwaitIndexedAsync(LakeItem item, CancellationToken token)
    {
        var latest = await FindLatestAsync(item.ProjectId, item.Key, token);
        for (var poll = 0; poll < indexPolls && !Reaches(latest, item.Version); poll++)
        {
            await Task.Delay(IndexPollDelay, token);
            latest = await FindLatestAsync(item.ProjectId, item.Key, token);
        }

        return latest;
    }

    private async Task<LakeWriteResult> FinishAsync(LakeItem item, LakeRetention retention, CancellationToken token)
    {
        if (retention != LakeRetention.LatestOnly) return new LakeWriteResult { Item = item, NewVersion = true };

        var survivor = await KeepOnlyLatestAsync(item, token);
        return new LakeWriteResult { Item = survivor, NewVersion = survivor.Version == item.Version };
    }

    private async Task<LakeWriteResult> TouchAsync(LakeItem latest, DateTime harvestedAt, CancellationToken token)
    {
        var checkedAt = ToStoredTime(harvestedAt);
        var filter = Builders<LakeItem>.Filter.Eq(i => i.Id, latest.Id);
        var update = Builders<LakeItem>.Update.Max(i => i.CheckedAt, checkedAt);
        var options = new FindOneAndUpdateOptions<LakeItem> { ReturnDocument = ReturnDocument.After };
        var touched = await items.FindOneAndUpdateAsync(filter, update, options, token);
        var item = touched ?? latest with { CheckedAt = Later(latest.CheckedAt, checkedAt) };
        return new LakeWriteResult { Item = item, NewVersion = false };
    }

    private async Task<LakeItem> KeepOnlyLatestAsync(LakeItem written, CancellationToken token)
    {
        var filter = KeyFilter(written.ProjectId, written.Key);
        var versions = await items.Find(filter).Sort(NewestFirst).ToListAsync(token);
        if (versions.Count == 0) return written;

        var stale = versions.Skip(1).ToList();
        if (stale.Count == 0) return versions[0];

        var staleIds = stale.Select(i => i.Id).ToList();
        await items.DeleteManyAsync(Builders<LakeItem>.Filter.In(i => i.Id, staleIds), token);
        foreach (var item in stale) LakeBody.TryDelete(PathOf(item));
        return versions[0];
    }

    private async Task<LakeItem?> FindLatestAsync(Guid projectId, string key, CancellationToken token)
    {
        var latest = items.Find(KeyFilter(projectId, key)).Sort(NewestFirst).Limit(1);
        return await latest.FirstOrDefaultAsync(token);
    }

    private static FilterDefinition<LakeItem> KeyFilter(Guid projectId, string key)
    {
        var filter = Builders<LakeItem>.Filter;
        return filter.Eq(i => i.ProjectId, projectId) & filter.Eq(i => i.Key, key);
    }

    private static FilterDefinition<LakeItem> QueryFilter(LakeQuery query)
    {
        var builder = Builders<LakeItem>.Filter;
        var filter = builder.Eq(i => i.ProjectId, query.ProjectId);
        if (!string.IsNullOrEmpty(query.Source)) filter &= builder.Eq(i => i.Source, query.Source);
        if (query.Kinds.Count > 0) filter &= builder.In(i => i.Kind, query.Kinds);
        return filter;
    }

    private static bool IsUnchanged([NotNullWhen(true)] LakeItem? latest, string hash, LakeRetention retention)
    {
        return retention == LakeRetention.ChangesOnly && latest is not null && latest.Hash == hash;
    }

    private static bool Reaches(LakeItem? latest, int version) => latest is not null && latest.Version >= version;

    private static DateTime Later(DateTime first, DateTime second) => first > second ? first : second;

    private static bool Matches(LakeItem item, byte[] body)
    {
        return item.Hash.Length == 0 || LakeBody.Hash(body) == item.Hash;
    }

    private static RawItem Verified(LakeItem item, byte[] body)
    {
        if (!Matches(item, body))
        {
            throw new InvalidDataException($"Lake body of {item.Key} version {item.Version} does not match its hash");
        }

        return new RawItem { Item = item, Body = body };
    }

    private static FileNotFoundException MissingBody(LakeItem item)
    {
        return new FileNotFoundException($"Lake body of {item.Key} version {item.Version} is missing", item.Location);
    }

    private static LakeItem NewItem(LakeWrite write, LakeKey lakeKey, int version, string hash)
    {
        var key = lakeKey.ToString();
        return new LakeItem
        {
            Id = DeterministicId.Create(write.ProjectId, $"lake:{key}:{version}"),
            ProjectId = write.ProjectId,
            Key = key,
            Source = write.Source,
            Kind = write.Kind,
            ProviderId = write.ProviderId,
            Version = version,
            Url = write.Url,
            ContentType = write.ContentType,
            HarvestedAt = ToStoredTime(write.HarvestedAt),
            CheckedAt = ToStoredTime(write.HarvestedAt),
            Hash = hash,
            Size = write.Body.LongLength,
            Location = LakePath.Location(lakeKey, version),
        };
    }

    private static DateTime ToStoredTime(DateTime value)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        if (value.Kind == DateTimeKind.Local) utc = value.ToUniversalTime();
        var ticks = utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond;
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private static void Validate(LakeWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(write.Body);
        ArgumentException.ThrowIfNullOrEmpty(write.Source);
        ArgumentException.ThrowIfNullOrEmpty(write.Kind);
        ArgumentException.ThrowIfNullOrEmpty(write.ProviderId);
        if (write.ProjectId == Guid.Empty) throw new ArgumentException("A lake write needs a project", nameof(write));
        if (write.Source.Contains('/')) throw new ArgumentException("A lake source cannot contain '/'", nameof(write));
        if (write.Kind.Contains('/')) throw new ArgumentException("A lake kind cannot contain '/'", nameof(write));
    }
}
