using System.Text;
using Collector.Core.Platform;

namespace Collector.Harvesting.Tests.Fakes;

internal sealed class RecordingFetcher : ISourceFetcher
{
    private readonly TimeProvider time;
    private readonly TimeSpan latency;

    public RecordingFetcher(TimeProvider time)
        : this(time, TimeSpan.Zero)
    {
    }

    public RecordingFetcher(TimeProvider time, TimeSpan latency)
    {
        this.time = time;
        this.latency = latency;
    }

    public List<TimeSpan> Starts { get; } = [];

    public List<Uri> Urls { get; } = [];

    public List<Source> Registered { get; } = [];

    public void Register(Source source)
    {
        Registered.Add(source);
    }

    public async Task<FetchResponse> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        Starts.Add(time.GetUtcNow() - Clock.Start);
        Urls.Add(url);
        if (latency > TimeSpan.Zero) await Task.Delay(latency, time, cancellationToken);

        var body = Encoding.UTF8.GetBytes($"page {url}");
        return new FetchResponse { StatusCode = 200, Url = url, ContentType = "text/html", Body = body };
    }
}
