namespace Collector.Core.Domains;

public sealed record ShoeFields : DomainFields
{
    public string? Colourway { get; init; }
    public string? ModelCode { get; init; }
}
