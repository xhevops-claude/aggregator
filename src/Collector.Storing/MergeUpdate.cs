using System.Diagnostics.CodeAnalysis;
using Collector.Core.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Collector.Storing;

internal sealed class MergeUpdate
{
    private static readonly HashSet<string> StoreOwned =
    [
        Elements.Id,
        Elements.ProjectId,
        Elements.CreatedAt,
        Elements.UpdatedAt,
        Elements.Aliases,
        Elements.AliasClaims,
        Elements.AnyListingInStock,
    ];

    private static readonly HashSet<string> Identity = [Elements.Namespace, Elements.Provider, Elements.ProviderId];

    private static readonly HashSet<string> ForwardOnly =
    [
        Elements.HarvestedAt,
        Elements.InfoCheckedAt,
        Elements.StockCheckedAt,
    ];

    private static readonly long DefaultDate = BsonConstants.DateTimeMinValueMillisecondsSinceEpoch;

    private readonly BsonDocument staged;
    private readonly BsonDocument insert = new();
    private readonly BsonDocument set = new();
    private readonly BsonDocument max = new();
    private readonly BsonDocument addToSet = new();
    private readonly BsonDocument unset = new();

    private MergeUpdate(BsonDocument staged)
    {
        this.staged = staged;
    }

    public static MergeUpdate From(Entity entity, Guid projectId, DateTime now, params string[] handledByCaller)
    {
        var update = new MergeUpdate(entity.ToBsonDocument(entity.GetType()));
        update.insert[Elements.ProjectId] = ToBson(projectId);
        update.insert[Elements.CreatedAt] = new BsonDateTime(now);
        update.set[Elements.UpdatedAt] = new BsonDateTime(now);

        foreach (var element in update.staged)
        {
            if (StoreOwned.Contains(element.Name) || handledByCaller.Contains(element.Name)) continue;

            if (Identity.Contains(element.Name)) update.insert[element.Name] = element.Value;
            else if (ForwardOnly.Contains(element.Name)) update.MaxWhenValued(element.Name, element.Value);
            else update.SetWhenValued(element.Name, element.Value);
        }

        return update;
    }

    public static BsonValue ToBson(Guid id) => new BsonBinaryData(id, GuidRepresentation.Standard);

    public static string AliasClaim(Guid alias) => $"{Elements.AliasClaims}.{alias}";

    public static bool IsValued(BsonDocument? stored, string path)
    {
        BsonValue? current = stored;
        foreach (var part in path.Split('.'))
        {
            if (current is not BsonDocument document || !document.TryGetValue(part, out current)) return false;
        }

        return current is not null && HasValue(current);
    }

    public bool IsOlderThan([NotNullWhen(true)] BsonDocument? stored)
    {
        var stagedAt = HarvestedAt(staged);
        var storedAt = HarvestedAt(stored);
        return stagedAt is not null && storedAt is not null && stagedAt < storedAt;
    }

    public void KeepStoredValues(BsonDocument stored)
    {
        unset.Clear();
        foreach (var path in set.Names.ToList())
        {
            if (path != Elements.UpdatedAt && IsValued(stored, path)) set.Remove(path);
        }
    }

    public void Insert(string name, BsonValue value) => insert[name] = value;

    public void Set(string name, BsonValue value) => set[name] = value;

    public void CopyStaged(string name)
    {
        if (staged.TryGetValue(name, out var value)) set[name] = value;
    }

    public void Unset(string name) => unset[name] = "";

    public void AddToSet(string name, IEnumerable<Guid> ids)
    {
        var values = new BsonArray(ids.Select(ToBson));
        if (values.Count > 0) addToSet[name] = new BsonDocument("$each", values);
    }

    public void ClaimAlias(Guid alias)
    {
        if (staged.TryGetValue(Elements.HarvestedAt, out var claimedAt)) MaxWhenValued(AliasClaim(alias), claimedAt);
    }

    public UpdateDefinition<T> ToDefinition<T>()
    {
        var update = new BsonDocument { { "$setOnInsert", insert }, { "$set", set } };
        if (max.ElementCount > 0) update["$max"] = max;
        if (addToSet.ElementCount > 0) update["$addToSet"] = addToSet;
        if (unset.ElementCount > 0) update["$unset"] = unset;
        return new BsonDocumentUpdateDefinition<T>(update);
    }

    private void MaxWhenValued(string name, BsonValue value)
    {
        if (HasValue(value)) max[name] = value;
    }

    private void SetWhenValued(string name, BsonValue value)
    {
        if (value is not BsonDocument document)
        {
            if (HasValue(value)) set[name] = value;
            return;
        }

        var parts = document.Where(part => part.Name != Elements.Discriminator && HasValue(part.Value)).ToList();
        if (parts.Count == 0) return;

        if (document.TryGetValue(Elements.Discriminator, out var discriminator))
        {
            set[$"{name}.{Elements.Discriminator}"] = discriminator;
        }

        foreach (var part in parts) set[$"{name}.{part.Name}"] = part.Value;
    }

    private static long? HarvestedAt(BsonDocument? document)
    {
        if (document is null || !document.TryGetValue(Elements.HarvestedAt, out var value)) return null;
        if (value is not BsonDateTime date || !HasValue(date)) return null;
        return date.MillisecondsSinceEpoch;
    }

    private static bool HasValue(BsonValue value)
    {
        if (value is BsonNull) return false;
        if (value is BsonString text) return !string.IsNullOrWhiteSpace(text.Value);
        if (value is BsonArray array) return array.Count > 0;
        if (value is BsonDocument document) return document.ElementCount > 0;
        if (value is BsonDateTime date) return date.MillisecondsSinceEpoch != DefaultDate;
        return true;
    }
}
