using Collector.Harvesting;

namespace Collector.Providers.Nbshop.Tests;

public sealed class FakeFetcher : IFetcher
{
    private readonly Dictionary<string, Func<Uri, FetchResponse>> routes = new(StringComparer.Ordinal);

    public List<string> Requests { get; } = [];

    public FakeFetcher Serve(string url, string sample, string contentType = "text/html; charset=UTF-8")
    {
        var body = SampleFiles.Bytes(sample);
        routes[url] = requested => Ok(requested, contentType, body);
        return this;
    }

    public FakeFetcher Answer(string url, int statusCode)
    {
        routes[url] = requested => new FetchResponse { StatusCode = statusCode, Url = requested };
        return this;
    }

    public FakeFetcher Fail(string url, Exception exception)
    {
        routes[url] = _ => throw exception;
        return this;
    }

    public Task<FetchResponse> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        var key = url.AbsoluteUri;
        Requests.Add(key);
        if (routes.TryGetValue(key, out var route)) return Task.FromResult(route(url));

        return Task.FromResult(new FetchResponse { StatusCode = 404, Url = url });
    }

    private static FetchResponse Ok(Uri url, string contentType, byte[] body)
    {
        return new FetchResponse
        {
            StatusCode = 200,
            Url = url,
            ContentType = contentType,
            Body = body,
        };
    }
}
