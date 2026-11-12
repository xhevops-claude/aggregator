using Collector.Harvesting;

namespace Collector.Providers.SportM;

internal sealed record SportMCatalogQueries
{
    public List<string> Queries { get; init; } = [];
    public string? ErrorClass { get; init; }
    public string ErrorText { get; init; } = "";
    public string ErrorProviderId { get; init; } = "";

    public static SportMCatalogQueries Resolve(HarvestRequest request)
    {
        var listing = request.Parameter(HarvestParameters.Listing);
        var brand = request.Parameter(HarvestParameters.Brand);

        var queries = SportMShoeListings.For(request.Source);
        if (!string.IsNullOrWhiteSpace(listing)) queries = SportMShoeListings.Split(listing);

        if (queries.Count == 0)
        {
            var text = $"no listing given and no shoe listing known for provider '{request.Source.Provider}'";
            return Error("no-listing", text, "listing");
        }

        if (string.IsNullOrWhiteSpace(brand)) return new SportMCatalogQueries { Queries = [.. queries] };

        var code = SportMBrands.CodeFor(brand);
        if (code is null) return Error("unknown-brand", $"no Brend code for brand '{brand}'", $"brand={brand}");

        var branded = queries.Select(query => SportMQuery.With(query, "Brend", code)).Distinct().ToList();
        return new SportMCatalogQueries { Queries = branded };
    }

    private static SportMCatalogQueries Error(string errorClass, string text, string providerId)
    {
        return new SportMCatalogQueries { ErrorClass = errorClass, ErrorText = text, ErrorProviderId = providerId };
    }
}
