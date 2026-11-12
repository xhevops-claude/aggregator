using Collector.Harvesting;

namespace Collector.Providers.SportM.Tests;

internal sealed class SampleFetcher : IFetcher
{
    private readonly Dictionary<string, string> samples = [];
    private readonly Dictionary<string, int> statuses = [];
    private readonly Dictionary<string, Exception> failures = [];

    public List<string> Requested { get; } = [];
    public Exception? FailEverything { get; set; }

    public void Serve(string url, string sampleName) => samples[url] = sampleName;

    public void Status(string url, int statusCode) => statuses[url] = statusCode;

    public void Throw(string url, Exception exception) => failures[url] = exception;

    public Task<FetchResponse> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        var key = url.AbsoluteUri;
        Requested.Add(key);

        if (FailEverything is not null) throw FailEverything;
        if (failures.TryGetValue(key, out var exception)) throw exception;

        if (statuses.TryGetValue(key, out var status))
        {
            return Task.FromResult(new FetchResponse { StatusCode = status, Url = url });
        }

        if (!samples.TryGetValue(key, out var sample))
        {
            return Task.FromResult(new FetchResponse { StatusCode = 404, Url = url });
        }

        var response = new FetchResponse
        {
            StatusCode = 200,
            Url = url,
            ContentType = "text/html; charset=utf-8",
            Body = SampleFiles.Bytes(sample),
        };
        return Task.FromResult(response);
    }
}
