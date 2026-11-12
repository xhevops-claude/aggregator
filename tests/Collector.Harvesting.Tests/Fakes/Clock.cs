namespace Collector.Harvesting.Tests.Fakes;

internal sealed class Clock
{
    public static readonly DateTimeOffset Start = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

    private const int MaxIdleWaits = 5_000;
    private const int MaxAdvances = 100_000;

    public TrackingTimeProvider Time { get; } = new(Start);

    public TimeSpan Elapsed => Time.GetUtcNow() - Start;

    public Task<T> Drive<T>(Func<Task<T>> start)
    {
        return Task.Run(() => DriveAsync(start));
    }

    private async Task<T> DriveAsync<T>(Func<Task<T>> start)
    {
        var task = start();
        var idleWaits = 0;
        var advances = 0;
        while (!task.IsCompleted)
        {
            var next = Time.NextDue();
            if (next is null)
            {
                if (++idleWaits > MaxIdleWaits) throw new TimeoutException("Nothing is due and the task is not done");
                await Task.Delay(1);
                continue;
            }

            if (++advances > MaxAdvances) throw new TimeoutException("The task did not finish while time was driven");
            var delta = next.Value - Time.GetUtcNow();
            Time.Advance(delta > TimeSpan.Zero ? delta : TimeSpan.Zero);
            await Task.Yield();
        }

        return await task;
    }
}
