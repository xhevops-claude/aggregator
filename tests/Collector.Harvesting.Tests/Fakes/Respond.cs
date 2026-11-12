using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Collector.Harvesting.Tests.Fakes;

internal static class Respond
{
    public static Responder Ok(string body = "ok")
    {
        return Status(HttpStatusCode.OK, body);
    }

    public static Responder Status(HttpStatusCode status, string body = "", string mediaType = "text/html")
    {
        return (_, _) => Task.FromResult(Message(status, body, mediaType));
    }

    public static Responder RetryAfter(HttpStatusCode status, TimeSpan delay)
    {
        Task<HttpResponseMessage> Answer(HttpRequestMessage request, CancellationToken token)
        {
            var response = Message(status, "", "text/plain");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(delay);
            return Task.FromResult(response);
        }

        return Answer;
    }

    public static Responder RetryAt(HttpStatusCode status, DateTimeOffset date)
    {
        Task<HttpResponseMessage> Answer(HttpRequestMessage request, CancellationToken token)
        {
            var response = Message(status, "", "text/plain");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(date);
            return Task.FromResult(response);
        }

        return Answer;
    }

    public static Responder Fails()
    {
        return (_, _) => throw new HttpRequestException("connection reset by peer");
    }

    public static Responder Hangs()
    {
        async Task<HttpResponseMessage> Answer(HttpRequestMessage request, CancellationToken token)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Message(HttpStatusCode.OK, "too late", "text/plain");
        }

        return Answer;
    }

    public static Responder Slow(TimeProvider time, TimeSpan latency)
    {
        async Task<HttpResponseMessage> Answer(HttpRequestMessage request, CancellationToken token)
        {
            await Task.Delay(latency, time, token);
            return Message(HttpStatusCode.OK, "slow", "text/plain");
        }

        return Answer;
    }

    public static Responder Redirected(string finalUrl, string body)
    {
        Task<HttpResponseMessage> Answer(HttpRequestMessage request, CancellationToken token)
        {
            var response = Message(HttpStatusCode.OK, body, "text/html");
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUrl);
            return Task.FromResult(response);
        }

        return Answer;
    }

    private static HttpResponseMessage Message(HttpStatusCode status, string body, string mediaType)
    {
        var content = new StringContent(body, Encoding.UTF8, mediaType);
        return new HttpResponseMessage(status) { Content = content };
    }
}
