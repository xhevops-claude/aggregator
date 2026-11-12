using Collector.Core.Lake;

namespace Collector.Lake.Tests;

public class LakeItemDatesTests
{
    private static readonly DateTime Harvested = Writes.Harvested;

    [Fact]
    public void An_item_without_a_check_was_last_checked_when_it_was_harvested()
    {
        var item = new LakeItem { HarvestedAt = Harvested };

        Assert.Equal(Harvested, item.LastCheckedAt());
    }

    [Fact]
    public void A_later_check_wins_over_the_harvest_time()
    {
        var item = new LakeItem { HarvestedAt = Harvested, CheckedAt = Harvested.AddDays(2) };

        Assert.Equal(Harvested.AddDays(2), item.LastCheckedAt());
    }

    [Fact]
    public void An_earlier_check_never_goes_back_before_the_harvest_time()
    {
        var item = new LakeItem { HarvestedAt = Harvested, CheckedAt = Harvested.AddDays(-2) };

        Assert.Equal(Harvested, item.LastCheckedAt());
    }
}
