using System.Collections.Concurrent;
using System.Net;
using System.Runtime.ExceptionServices;
using Collector.Core.Platform;

namespace Collector.Harvesting;

public sealed class PoliteFetcher : ISourceFetcher, IDisposable
{
    private readonly HttpClient client;
    private readonly FetcherOptions options;
    private readonly TimeProvider time;
    private readonly HostSettingsTable settings;
    private readonly ConcurrentDictionary<string, HostState> hosts = new(StringComparer.OrdinalIgnoreCase);

    public PoliteFetcher(FetcherOptions options, TimeProvider time)
        : this(CreateHandler(), options, time)
    {
    }

    public PoliteFetcher(HttpMessageHandler handler, FetcherOptions options, TimeProvider time)
    {
        this.options = options;
        this.time = time;
        settings = new HostSettingsTable(options);
        client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", options.Accept);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", options.AcceptLanguage);
    }

    public void Register(Source source)
    {
        if (!Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var baseUrl))
        {
            throw new ArgumentException($"Source '{source.Key}' has no absolute base url", nameof(source));
        }

        settings.Register(HostKey(baseUrl), $"{source.ProjectId}/{source.Key}", HostSettings.From(source));
    }

    public async Task<FetchResponse> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!url.IsAbsoluteUri) throw new ArgumentException($"'{url}' is not an absolute url", nameof(url));

        var key = HostKey(url);
        var host = hosts.GetOrAdd(key, _ => new HostState());
        var rules = settings.For(key);
        if (rules.RespectRobots)
        {
            var robots = await RobotsAsync(url, host, rules, cancellationToken);
            if (!robots.IsAllowed(url)) throw new RobotsDisallowedException(url);
        }

        return await SendAsync(url, host, rules, cancellationToken);
    }

    public void Dispose()
    {
        client.Dispose();
    }

    private static SocketsHttpHandler CreateHandler()
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All,
            MaxConnectionsPerServer = 1,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
    }

    private static string HostKey(Uri url)
    {
        return url.Authority.ToLowerInvariant();
    }

    private async Task<RobotsRules> RobotsAsync(Uri url, HostState host, HostSettings rules, CancellationToken token)
    {
        if (host.Robots is { } cached) return cached;

        await host.RobotsGate.WaitAsync(token);
        try
        {
            if (host.Robots is { } loaded) return loaded;
            if (host.RobotsFailure is { } failure && failure.Until > time.GetUtcNow())
            {
                throw new RobotsDisallowedException(url, failure.Reason);
            }

            host.Robots = await LoadRobotsAsync(url, host, rules, token);
            host.RobotsFailure = null;
            return host.Robots;
        }
        finally
        {
            host.RobotsGate.Release();
        }
    }

    private async Task<RobotsRules> LoadRobotsAsync(Uri url, HostState host, HostSettings rules,
        CancellationToken token)
    {
        FetchResponse response;
        try
        {
            response = await SendAsync(new Uri(url, "/robots.txt"), host, rules, token);
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutException)
        {
            throw Unreachable(url, host, $"could not be read ({exception.Message})", exception);
        }

        if (response.IsSuccess) return RobotsRules.Parse(response.Text);
        if (response.StatusCode is >= 400 and < 500 and not 429) return RobotsRules.AllowAll;
        throw Unreachable(url, host, $"answered {response.StatusCode}", null);
    }

    private RobotsDisallowedException Unreachable(Uri url, HostState host, string problem, Exception? cause)
    {
        var reason = $"robots.txt of {url.Authority} {problem}, the host is treated as disallowed";
        host.RobotsFailure = new RobotsFailure(reason, time.GetUtcNow() + options.UnreachableRobotsHold);
        return new RobotsDisallowedException(url, reason, cause);
    }

    private async Task<FetchResponse> SendAsync(Uri url, HostState host, HostSettings rules, CancellationToken token)
    {
        await host.Gate.WaitAsync(token);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                await WaitForHostAsync(url, host, token);
                var result = await AttemptAsync(url, token);
                var now = time.GetUtcNow();
                host.NextAllowed = now + rules.MinRequestInterval;
                if (result.Response is { IsSuccess: false } && result.RetryAfter is { } retryAfter)
                {
                    Hold(host, now + retryAfter);
                }

                var delay = RetryDelay(result, attempt);
                if (delay is null) return Outcome(result);

                var retryAt = now + delay.Value;
                if (retryAt > host.NextAllowed) host.NextAllowed = retryAt;
            }
        }
        finally
        {
            host.Gate.Release();
        }
    }

    private async Task WaitForHostAsync(Uri url, HostState host, CancellationToken token)
    {
        var now = time.GetUtcNow();
        if (host.HeldUntil - now > options.MaxRetryAfter) throw new HostRateLimitedException(url, host.HeldUntil);

        var next = host.NextAllowed > host.HeldUntil ? host.NextAllowed : host.HeldUntil;
        var wait = next - now;
        if (wait > TimeSpan.Zero) await Task.Delay(wait, time, token);
    }

    private static void Hold(HostState host, DateTimeOffset until)
    {
        if (until > host.HeldUntil) host.HeldUntil = until;
    }

    private async Task<FetchAttempt> AttemptAsync(Uri url, CancellationToken token)
    {
        using var timeout = new CancellationTokenSource(options.Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
        try
        {
            using var response = await client.GetAsync(url, linked.Token);
            var body = await response.Content.ReadAsByteArrayAsync(linked.Token);
            var fetched = new FetchResponse
            {
                StatusCode = (int)response.StatusCode,
                Url = response.RequestMessage?.RequestUri ?? url,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "",
                Body = body,
            };
            return new FetchAttempt { Response = fetched, RetryAfter = RetryAfter(response) };
        }
        catch (HttpRequestException exception)
        {
            return new FetchAttempt { Error = exception };
        }
        catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
        {
            var message = $"GET {url} timed out after {options.Timeout.TotalSeconds:0} s";
            return new FetchAttempt { Error = new TimeoutException(message, exception) };
        }
    }

    private TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta) return delta;
        if (header?.Date is not { } date) return null;

        var wait = date - time.GetUtcNow();
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    private TimeSpan? RetryDelay(FetchAttempt result, int attempt)
    {
        if (attempt >= options.Attempts) return null;

        var backoff = options.RetryDelay * Math.Pow(2, attempt - 1);
        if (result.Error is not null) return backoff;
        if (!IsTransient(result.Response!.StatusCode)) return null;
        if (result.RetryAfter is not { } retryAfter) return backoff;
        return retryAfter <= options.MaxRetryAfter ? retryAfter : null;
    }

    private static bool IsTransient(int status)
    {
        return status is >= 500 and < 600 or 429;
    }

    private static FetchResponse Outcome(FetchAttempt result)
    {
        if (result.Error is not null) ExceptionDispatchInfo.Throw(result.Error);
        return result.Response!;
    }
}
