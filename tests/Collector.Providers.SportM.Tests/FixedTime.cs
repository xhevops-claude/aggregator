namespace Collector.Providers.SportM.Tests;

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTime Now = new(2026, 11, 12, 1, 30, 0, DateTimeKind.Utc);

    public FixedTime() : this(new DateTimeOffset(Now))
    {
    }

    public override DateTimeOffset GetUtcNow() => now;
}
