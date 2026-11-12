using Collector.Core.Platform;

namespace Collector.Harvesting;

public sealed class PacedFetcher : IFetcher
{
    private readonly IFetcher inner;
    private readonly Pacing pacing;
    private readonly int? expectedRequests;
    private readonly HostBudget budget;
    private readonly TimeProvider time;
    private readonly Random random;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset? lastStart;

    public PacedFetcher(IFetcher inner, Pacing pacing, int? expectedRequests, HostBudget budget, TimeProvider time)
        : this(inner, pacing, expectedRequests, budget, time, Random.Shared)
    {
    }

    public PacedFetcher(IFetcher inner, Pacing pacing, int? expectedRequests, HostBudget budget, TimeProvider time,
        Random random)
    {
        this.inner = inner;
        this.pacing = pacing;
        this.expectedRequests = expectedRequests;
        this.budget = budget;
        this.time = time;
        this.random = random;
    }

    public async Task<FetchResponse> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await WaitForTurnAsync(url, cancellationToken);
            lastStart = time.GetUtcNow();
            return await inner.GetAsync(url, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task WaitForTurnAsync(Uri url, CancellationToken cancellationToken)
    {
        var next = AfterPrevious();
        if (BudgetPerHour() is { } maxPerHour) next = budget.Reserve(url, maxPerHour, next);

        var wait = next - time.GetUtcNow();
        if (wait > TimeSpan.Zero) await Task.Delay(wait, time, cancellationToken);
    }

    private DateTimeOffset AfterPrevious()
    {
        if (lastStart is not { } start) return DateTimeOffset.MinValue;
        return start + StrategyGap();
    }

    private int? BudgetPerHour()
    {
        if (pacing.Mode != PacingMode.Budget || pacing.MaxPerHour is not > 0) return null;
        return pacing.MaxPerHour;
    }

    private TimeSpan StrategyGap()
    {
        var interval = pacing.Interval ?? TimeSpan.Zero;
        return pacing.Mode switch
        {
            PacingMode.Burst => TimeSpan.Zero,
            PacingMode.Jitter => interval + RandomJitter(),
            PacingMode.Spread => SpreadGap(interval) + RandomJitter(),
            _ => interval,
        };
    }

    private TimeSpan SpreadGap(TimeSpan interval)
    {
        if (pacing.Window is not { } window || expectedRequests is not > 0) return interval;
        return window / expectedRequests.Value;
    }

    private TimeSpan RandomJitter()
    {
        if (pacing.Jitter is not { } jitter || jitter <= TimeSpan.Zero) return TimeSpan.Zero;
        return TimeSpan.FromTicks((long)(random.NextDouble() * jitter.Ticks));
    }
}
