using Collector.Core.Domains;

namespace Collector.Core.Entities;

public sealed record Product : Entity
{
    public string Name { get; init; } = "";
    public string? Brand { get; init; }
    public string? Sku { get; init; }
    public Guid? CategoryId { get; init; }
    public List<string> Images { get; init; } = [];
    public bool? AnyListingInStock { get; init; }
    public List<Guid> Aliases { get; init; } = [];
    public DomainFields? Fields { get; init; }
}
