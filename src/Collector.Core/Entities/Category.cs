namespace Collector.Core.Entities;

public sealed record Category : Entity
{
    public string Name { get; init; } = "";
    public Guid? ParentId { get; init; }
    public string Path { get; init; } = "";
}
