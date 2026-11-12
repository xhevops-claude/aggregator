using System.Text;
using Collector.Core.Lake;

namespace Collector.Lake;

public sealed record RawItem
{
    public required LakeItem Item { get; init; }
    public byte[] Body { get; init; } = [];

    public string Text => Encoding.UTF8.GetString(Body);
}
