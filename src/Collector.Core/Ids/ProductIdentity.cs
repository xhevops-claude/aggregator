namespace Collector.Core.Ids;

public static class ProductIdentity
{
    public static Guid Candidate(Guid projectSeed, string provider, string providerId) =>
        DeterministicId.For(projectSeed, provider, "product", providerId);

    public static Guid? Canonical(Guid projectSeed, string? brand, string? sku)
    {
        if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(sku)) return null;

        var brandKey = brand.Trim().ToLowerInvariant();
        var skuKey = sku.Trim().ToUpperInvariant();
        return DeterministicId.Create(projectSeed, $"product:{brandKey}:{skuKey}");
    }
}
