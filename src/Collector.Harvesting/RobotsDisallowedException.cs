namespace Collector.Harvesting;

public sealed class RobotsDisallowedException : Exception
{
    public RobotsDisallowedException(Uri url)
        : this(url, $"robots.txt disallows {url}")
    {
    }

    public RobotsDisallowedException(Uri url, string message)
        : this(url, message, null)
    {
    }

    public RobotsDisallowedException(Uri url, string message, Exception? innerException)
        : base(message, innerException)
    {
        Url = url;
    }

    public Uri Url { get; }
}
