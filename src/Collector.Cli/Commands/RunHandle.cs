namespace Collector.Cli.Commands;

public sealed record RunHandle(Guid RunId, Guid ItemId, Guid CommandId);
