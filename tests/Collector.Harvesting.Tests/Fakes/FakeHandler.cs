using System.Net;

namespace Collector.Harvesting.Tests.Fakes;

internal sealed class FakeHandler : HttpMessageHandler
{
    private readonly TimeProvider time;
    private readonly Dictionary<string, Queue<Responder>> routes = [];
    private readonly Lock gate = new();
    private readonly List<SeenRequest> seen = [];
    private int inFlight;

    public FakeHandler(TimeProvider time)
    {
        this.time = time;
    }

    public int MostInFlight { get; private set; }

    public IReadOnlyList<SeenRequest> Seen
    {
        get
        {
            lock (gate) return seen.ToList();
        }
    }

    public List<TimeSpan> Times => Seen.Select(request => request.At).ToList();

    public List<string> Urls => Seen.Select(request => request.Url.ToString()).ToList();

    public void On(string url, params Responder[] responders)
    {
        routes[url] = new Queue<Responder>(responders);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!;
        var at = time.GetUtcNow() - Clock.Start;
        var userAgent = Header(request, "User-Agent");
        var language = Header(request, "Accept-Language");
        Responder responder;
        lock (gate)
        {
            seen.Add(new SeenRequest(url, at, userAgent, language));
            inFlight++;
            MostInFlight = Math.Max(MostInFlight, inFlight);
            responder = Next(url.ToString());
        }

        try
        {
            var response = await responder(request, cancellationToken);
            response.RequestMessage ??= request;
            return response;
        }
        finally
        {
            lock (gate) inFlight--;
        }
    }

    private Responder Next(string url)
    {
        if (!routes.TryGetValue(url, out var queue)) return Respond.Status(HttpStatusCode.NotFound);
        return queue.Count > 1 ? queue.Dequeue() : queue.Peek();
    }

    private static string Header(HttpRequestMessage request, string name)
    {
        return request.Headers.NonValidated.TryGetValues(name, out var values) ? values.ToString() : "";
    }
}
