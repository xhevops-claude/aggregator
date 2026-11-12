using Collector.Harvesting;

namespace Collector.Providers.SportM;

internal static class SportMItems
{
    private const string DefaultContentType = "text/html";

    public static HarvestItem Stored(string kind, string providerId, string url, SportMFetchResult result)
    {
        var response = result.Response!;
        var contentType = response.ContentType.Length == 0 ? DefaultContentType : response.ContentType;
        return new HarvestItem
        {
            Kind = kind,
            ProviderId = providerId,
            Url = url,
            ContentType = contentType,
            Body = response.Body,
            HarvestedAt = result.HarvestedAt,
        };
    }

    public static HarvestItem Failed(string kind, string providerId, string url, SportMFetchResult result)
    {
        return Failed(kind, providerId, url, result.ErrorClass!, result.ErrorText ?? "", result.HarvestedAt);
    }

    public static HarvestItem Failed(
        string kind, string providerId, string url, string errorClass, string errorText, DateTime harvestedAt)
    {
        return new HarvestItem
        {
            Kind = kind,
            ProviderId = providerId,
            Url = url,
            HarvestedAt = harvestedAt,
            ErrorClass = errorClass,
            ErrorText = errorText,
        };
    }
}
