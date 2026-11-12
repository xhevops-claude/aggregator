using Collector.Core.Platform;
using MongoDB.Driver;

namespace Collector.Data;

public sealed class LeafResolver(CollectorDatabase db)
{
    public async Task<ResolvedLeaf> ResolveAsync(Guid projectId, string path, CancellationToken token)
    {
        var project = await db.Projects.Find(p => p.Id == projectId).FirstOrDefaultAsync(token);
        if (project is null) throw new InvalidOperationException($"Unknown project {projectId}");

        var operations = await db.Operations.Find(o => o.ProjectId == projectId).ToListAsync(token);
        var operation = operations.FirstOrDefault(o => o.Path == path);
        if (operation is null) throw new InvalidOperationException($"Unknown operation {path}");
        if (operation.SourceId is null) throw new InvalidOperationException($"{path} is not a leaf operation");

        var sourceId = operation.SourceId.Value;
        var source = await db.Sources.Find(s => s.Id == sourceId).FirstOrDefaultAsync(token);
        if (source is null) throw new InvalidOperationException($"Source of {path} not found");

        var domain = DomainOf(operation, operations);
        if (domain is null) throw new InvalidOperationException($"No domain on {path} or its parents");
        return new ResolvedLeaf(project, operation, source, domain);
    }

    private static string? DomainOf(Operation operation, List<Operation> all)
    {
        var current = operation;
        while (current is not null)
        {
            if (current.Domain is not null) return current.Domain;
            current = all.FirstOrDefault(o => o.Id == current.ParentId);
        }

        return null;
    }
}
