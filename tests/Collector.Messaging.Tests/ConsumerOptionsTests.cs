namespace Collector.Messaging.Tests;

public class ConsumerOptionsTests
{
    [Fact]
    public void Retry_delay_doubles_from_one_second_by_default()
    {
        var options = new ConsumerOptions();

        var delays = Enumerable.Range(1, 4).Select(options.RetryDelayAfter);

        Assert.Equal(5, options.MaxAttempts);
        double[] expected = [1, 2, 4, 8];
        Assert.Equal(expected, delays.Select(delay => delay.TotalSeconds));
    }

    [Fact]
    public void Retry_delay_stops_growing_at_the_maximum()
    {
        var options = new ConsumerOptions { MaxRetryDelay = TimeSpan.FromSeconds(5) };

        Assert.Equal(TimeSpan.FromSeconds(5), options.RetryDelayAfter(4));
        Assert.Equal(TimeSpan.FromSeconds(5), options.RetryDelayAfter(200));
    }

    [Fact]
    public void Options_never_print_the_credentials()
    {
        var text = new RabbitMqOptions().ToString();

        Assert.DoesNotContain("collector:collector", text);
        Assert.Contains("localhost:5673", text);
    }
}
