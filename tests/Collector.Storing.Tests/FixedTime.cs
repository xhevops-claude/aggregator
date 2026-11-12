namespace Collector.Storing.Tests;

internal sealed class FixedTime : TimeProvider
{
    public static readonly DateTime Start = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    public DateTimeOffset Now { get; set; } = new(Start);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}
