using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Core.Messages;
using Collector.Core.Staging;
using Collector.Data;
using Collector.Lake;
using MongoDB.Driver;

namespace Collector.Adapting;

public sealed class AdaptRunner
{
    private readonly ILakeReader lake;
    private readonly CollectorDatabase database;
    private readonly IMessagePublisher publisher;
    private readonly TimeProvider time;

    public AdaptRunner(ILakeReader lake, CollectorDatabase database, IMessagePublisher publisher, TimeProvider time)
    {
        MongoMapping.Register();
        this.lake = lake;
        this.database = database;
        this.publisher = publisher;
        this.time = time;
    }

    public async Task<AdaptReport> RunAsync(AdaptInput input, CancellationToken cancellationToken)
    {
        Validate(input);

        var command = input.Command;
        var context = new AdaptContext
        {
            ProjectId = input.Project.Id,
            ProjectSeed = input.Project.Seed,
            Namespace = command.Namespace,
            Provider = input.Source.Provider,
            Source = input.Source,
            AdaptedAt = time.GetUtcNow().UtcDateTime,
        };
        var tally = new AdaptTally();

        if (command.LakeKeys.Count > 0)
        {
            foreach (var key in command.LakeKeys.Distinct(StringComparer.Ordinal))
            {
                var read = () => lake.ReadLatestAsync(command.ProjectId, key, cancellationToken);
                var raw = await TryReadAsync(key, read, tally);
                if (raw is not null) await AdaptRawAsync(input, context, raw, tally, cancellationToken);
            }
        }
        else
        {
            var query = new LakeQuery
            {
                ProjectId = command.ProjectId,
                Source = input.Source.Key,
                Kinds = [.. input.Adapter.RawKinds],
            };
            var items = await lake.LatestAsync(query, cancellationToken);
            foreach (var item in items)
            {
                Func<Task<RawItem?>> read = async () => await lake.ReadAsync(item, cancellationToken);
                var raw = await TryReadAsync(item.Key, read, tally);
                if (raw is not null) await AdaptRawAsync(input, context, raw, tally, cancellationToken);
            }
        }

        var report = tally.ToReport();
        await publisher.PublishAsync(MessageNames.ItemCompleted, Completed(command, report), cancellationToken);
        return report;
    }

    private static async Task<RawItem?> TryReadAsync(string key, Func<Task<RawItem?>> read, AdaptTally tally)
    {
        try
        {
            var raw = await read();
            if (raw is null) tally.Fail(key, "LakeItemMissing", $"No lake item with key {key}");
            return raw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            tally.Fail(key, exception);
            return null;
        }
    }

    private async Task AdaptRawAsync(
        AdaptInput input, AdaptContext context, RawItem raw, AdaptTally tally, CancellationToken cancellationToken)
    {
        if (!IsOwnRaw(input, raw.Item.Source, raw.Item.Kind))
        {
            tally.Skipped.Add(raw.Item.Key);
            return;
        }

        tally.RawItems++;
        List<Entity> entities;
        try
        {
            entities = input.Adapter.Adapt(raw, context).ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            tally.Fail(raw.Item.Key, exception);
            return;
        }

        var documents = new Dictionary<Guid, StagingDocument>();
        foreach (var entity in entities)
        {
            var kind = KindOf(entity);
            if (kind is null || !Belongs(entity, input.Command))
            {
                tally.Rejected++;
                continue;
            }

            var document = Stage(entity, kind.Value, raw, input.Command, context.AdaptedAt);
            documents[document.Id] = document;
        }

        await WriteAsync(documents.Values, cancellationToken);
        foreach (var document in documents.Values)
        {
            var adapted = Adapted(input.Command, document);
            await publisher.PublishAsync(MessageNames.EntityAdapted, adapted, cancellationToken);
            tally.StagingIds.Add(document.Id);
        }
    }

    private async Task WriteAsync(IEnumerable<StagingDocument> documents, CancellationToken cancellationToken)
    {
        var models = new List<WriteModel<StagingDocument>>();
        foreach (var document in documents)
        {
            var filter = Builders<StagingDocument>.Filter.Eq(staged => staged.Id, document.Id);
            models.Add(new ReplaceOneModel<StagingDocument>(filter, document) { IsUpsert = true });
        }

        if (models.Count == 0) return;

        var options = new BulkWriteOptions { IsOrdered = false };
        await database.Staging.BulkWriteAsync(models, options, cancellationToken);
    }

    private static StagingDocument Stage(Entity entity, EntityKind kind, RawItem raw, AdaptCommand command, DateTime at)
    {
        var name = $"staging:{entity.Id}:{raw.Item.Key}:{raw.Item.Version}";
        return new StagingDocument
        {
            Id = DeterministicId.Create(command.RunId, name),
            ProjectId = command.ProjectId,
            RunId = command.RunId,
            Kind = kind,
            EntityId = entity.Id,
            LakeKey = raw.Item.Key,
            LakeVersion = raw.Item.Version,
            AdaptedAt = at,
            State = StagingState.Pending,
            Entity = entity,
        };
    }

    private static EntityAdapted Adapted(AdaptCommand command, StagingDocument document)
    {
        return new EntityAdapted
        {
            ProjectId = command.ProjectId,
            RunId = command.RunId,
            ItemId = command.ItemId,
            StagingId = document.Id,
        };
    }

    private static ItemCompleted Completed(AdaptCommand command, AdaptReport report)
    {
        var first = report.Failures.FirstOrDefault();
        string? text = null;
        if (first is not null)
        {
            text = $"{report.Failures.Count} lake item(s) failed, first {first.LakeKey}: {first.ErrorText}";
        }

        return new ItemCompleted
        {
            ProjectId = command.ProjectId,
            RunId = command.RunId,
            ItemId = command.ItemId,
            State = report.State,
            ErrorClass = first?.ErrorClass,
            ErrorText = text,
        };
    }

    private static bool IsOwnRaw(AdaptInput input, string source, string kind)
    {
        var sameSource = string.Equals(source, input.Source.Key, StringComparison.Ordinal);
        return sameSource && input.Adapter.RawKinds.Contains(kind, StringComparer.Ordinal);
    }

    private static bool Belongs(Entity entity, AdaptCommand command)
    {
        var sameProject = entity.ProjectId == command.ProjectId;
        return sameProject && string.Equals(entity.Namespace, command.Namespace, StringComparison.Ordinal);
    }

    private static EntityKind? KindOf(Entity? entity)
    {
        if (entity is Product) return EntityKind.Product;
        if (entity is Listing) return EntityKind.Listing;
        if (entity is Category) return EntityKind.Category;
        return null;
    }

    private static void Validate(AdaptInput input)
    {
        var project = input.Project;
        var operation = input.Operation;
        Require(input.Command.ProjectId == project.Id, "command belongs to another project");
        Require(operation.ProjectId == project.Id, "operation belongs to another project");
        Require(input.Source.ProjectId == project.Id, "source belongs to another project");
        Require(operation.Path == input.Command.Namespace, "operation path differs from the command namespace");
        Require(operation.SourceId is null || operation.SourceId == input.Source.Id, "source is not the leaf's");
        Require(SameName(input.Adapter.Domain, input.Domain), "adapter serves another domain");
        Require(SameName(input.Adapter.Platform, input.Source.Platform), "adapter serves another platform");
        Require(input.Adapter.RawKinds.Count > 0, "adapter declares no raw kinds");
    }

    private static bool SameName(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ArgumentException($"Adapt input rejected: {message}");
    }
}
