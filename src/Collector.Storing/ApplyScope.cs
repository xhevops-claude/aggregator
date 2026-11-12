using MongoDB.Driver;

namespace Collector.Storing;

internal sealed record ApplyScope(IClientSessionHandle Session, Guid ProjectId, DateTime Now, CancellationToken Token);
