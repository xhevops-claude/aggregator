using Collector.Harvesting;

namespace Collector.Providers.Nbshop;

public sealed record NbshopFetchOutcome
{
    public const string RobotsErrorClass = "robots";

    public string RequestedUrl { get; init; } = "";
    public FetchResponse? Response { get; init; }
    public string? ErrorClass { get; init; }
    public string? ErrorText { get; init; }

    public bool Succeeded => ErrorClass is null && Response is not null;
    public int? StatusCode => Response?.StatusCode;

    public static async Task<NbshopFetchOutcome> GetAsync(
        IFetcher fetcher, string url, CancellationToken cancellationToken)
    {
        if (!NbshopUrl.TryHttp(url, out var uri)) return Failure(url, "bad-url", $"{url} is not an http(s) URL");

        FetchResponse response;
        try
        {
            response = await fetcher.GetAsync(uri, cancellationToken);
        }
        catch (RobotsDisallowedException exception)
        {
            return Failure(url, RobotsErrorClass, exception.Message);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(url, exception.GetType().Name, exception.Message);
        }

        if (response.IsSuccess) return new NbshopFetchOutcome { RequestedUrl = url, Response = response };

        return new NbshopFetchOutcome
        {
            RequestedUrl = url,
            Response = response,
            ErrorClass = $"http-{response.StatusCode}",
            ErrorText = $"{url} answered {response.StatusCode}",
        };
    }

    public static NbshopFetchOutcome Failure(string url, string errorClass, string errorText)
    {
        return new NbshopFetchOutcome
        {
            RequestedUrl = url,
            ErrorClass = errorClass,
            ErrorText = errorText,
        };
    }

    public HarvestItem ToItem(string kind, string providerId, DateTime harvestedAt)
    {
        if (Succeeded && Response is not null)
        {
            return new HarvestItem
            {
                Kind = kind,
                ProviderId = providerId,
                Url = Response.Url.ToString(),
                ContentType = Response.ContentType,
                Body = Response.Body,
                HarvestedAt = harvestedAt,
            };
        }

        return new HarvestItem
        {
            Kind = kind,
            ProviderId = providerId,
            Url = RequestedUrl,
            HarvestedAt = harvestedAt,
            ErrorClass = ErrorClass,
            ErrorText = ErrorText,
        };
    }
}
