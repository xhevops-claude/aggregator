using Collector.Core.Lake;

namespace Collector.Lake.Tests;

public class LakePathTests
{
    private const string SafeSegment = "^[A-Za-z0-9][A-Za-z0-9_-]*$";
    private const int MaxSegmentLength = LakePath.ReadablePrefixLength + 1 + LakePath.HashSuffixLength;
    private static readonly Guid ProjectId = Guid.Parse("0b7c1f8e-2d41-4c55-9f3a-1e2d3c4b5a69");

    [Theory]
    [InlineData("sportm_mk")]
    [InlineData("product")]
    [InlineData("listing-page")]
    [InlineData("sitemap.xml")]
    public void Plain_names_stay_readable(string name)
    {
        Assert.Equal(name, LakePath.NameSegment(name));
    }

    [Theory]
    [InlineData("Sport M")]
    [InlineData("Product")]
    [InlineData("con")]
    [InlineData("nul.txt")]
    [InlineData("com1")]
    [InlineData("lpt9")]
    [InlineData(".hidden")]
    [InlineData("trailing.")]
    [InlineData("a..b")]
    public void Names_that_are_not_plain_get_a_hash_suffix(string name)
    {
        var segment = LakePath.NameSegment(name);

        Assert.NotEqual(name, segment);
        Assert.Matches(SafeSegment, segment);
    }

    [Theory]
    [MemberData(nameof(NastyIds.Theory), MemberType = typeof(NastyIds))]
    public void Provider_id_segments_are_safe_on_windows_and_linux(string providerId)
    {
        var segment = LakePath.HashSuffixedSegment(providerId);

        Assert.Matches(SafeSegment, segment);
        Assert.InRange(segment.Length, LakePath.HashSuffixLength, MaxSegmentLength);
    }

    [Theory]
    [InlineData("nul\0char")]
    [InlineData("bell\a")]
    [InlineData("\u007f")]
    [InlineData("line\u2028separator")]
    public void Control_characters_never_reach_the_file_name(string providerId)
    {
        Assert.Matches(SafeSegment, LakePath.HashSuffixedSegment(providerId));
    }

    [Fact]
    public void Different_provider_ids_never_share_a_segment_even_ignoring_case()
    {
        var segments = NastyIds.All.Select(LakePath.HashSuffixedSegment).ToList();

        Assert.Equal(NastyIds.All.Count, NastyIds.All.Distinct().Count());
        Assert.Equal(segments.Count, segments.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void The_same_provider_id_always_gives_the_same_segment()
    {
        var first = LakePath.HashSuffixedSegment("https://www.sport-m.com.mk/Product.aspx?id=12");
        var second = LakePath.HashSuffixedSegment("https://www.sport-m.com.mk/Product.aspx?id=12");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("HP6005", "HP6005-")]
    [InlineData("a/b?c=d", "a_b_c_d-")]
    [InlineData("/products/123/", "products_123-")]
    public void The_readable_part_keeps_the_start_of_the_id(string providerId, string prefix)
    {
        Assert.StartsWith(prefix, LakePath.HashSuffixedSegment(providerId));
    }

    [Fact]
    public void An_id_without_readable_characters_is_the_hash_alone()
    {
        var segment = LakePath.HashSuffixedSegment("Патики");

        Assert.Equal(LakePath.HashSuffixLength, segment.Length);
    }

    [Fact]
    public void Location_is_project_source_kind_provider_id_and_version()
    {
        var key = new LakeKey(ProjectId, "sportm_mk", "product", "HP6005");
        var segment = LakePath.HashSuffixedSegment("HP6005");

        var location = LakePath.Location(key, 7);

        Assert.Equal($"{ProjectId}/sportm_mk/product/{segment}/000007.gz", location);
    }

    [Fact]
    public void Provider_ids_that_are_not_valid_unicode_are_rejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => LakePath.HashSuffixedSegment("a\uD800b"));
    }
}
