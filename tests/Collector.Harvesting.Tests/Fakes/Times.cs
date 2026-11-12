namespace Collector.Harvesting.Tests.Fakes;

internal static class Times
{
    public static TimeSpan[] Seconds(params double[] values)
    {
        return values.Select(value => TimeSpan.FromSeconds(value)).ToArray();
    }
}
