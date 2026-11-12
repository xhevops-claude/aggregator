using System.Text;

namespace Collector.Harvesting;

public sealed record FetchResponse
{
    public int StatusCode { get; init; }
    public required Uri Url { get; init; }
    public string ContentType { get; init; } = "";
    public byte[] Body { get; init; } = [];

    public bool IsSuccess => StatusCode is >= 200 and < 300;
    public string Text => Encoding.UTF8.GetString(Body);
}
