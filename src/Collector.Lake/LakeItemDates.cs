using Collector.Core.Lake;

namespace Collector.Lake;

public static class LakeItemDates
{
    public static DateTime LastCheckedAt(this LakeItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.CheckedAt > item.HarvestedAt ? item.CheckedAt : item.HarvestedAt;
    }
}
