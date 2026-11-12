namespace Collector.Harvesting.Tests;

public class RobotsRulesTests
{
    [Theory]
    [InlineData("/patike/nike-air-max-90", true)]
    [InlineData("/patike?search=nike", false)]
    [InlineData("/brendovi/nike?SID=abc", false)]
    [InlineData("/index.php", false)]
    [InlineData("/index.php?page=2", true)]
    [InlineData("/patike?iframeView=1", false)]
    [InlineData("/patike?iframeView=10", true)]
    [InlineData("/patike?crawl=no", false)]
    [InlineData("/login?back_url=/kosnica", false)]
    public void The_saved_sport_vision_file_blocks_only_search_and_session_urls(string path, bool allowed)
    {
        var rules = RobotsRules.Parse(SampleFile.Read("sportvision_mk.robots.txt"));

        Assert.Equal(allowed, rules.IsAllowed(Url(path)));
    }

    [Theory]
    [InlineData("/shop/sale/1", true)]
    [InlineData("/shop/new", false)]
    [InlineData("/about", true)]
    public void The_longest_match_wins(string path, bool allowed)
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /shop\nAllow: /shop/sale\n");

        Assert.Equal(allowed, rules.IsAllowed(Url(path)));
    }

    [Fact]
    public void A_longer_disallow_beats_a_shorter_allow()
    {
        var rules = RobotsRules.Parse("User-agent: *\nAllow: /shop\nDisallow: /shop/checkout\n");

        Assert.False(rules.IsAllowed(Url("/shop/checkout/pay")));
        Assert.True(rules.IsAllowed(Url("/shop/cart")));
    }

    [Fact]
    public void Allow_wins_a_tie()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /page\nAllow: /page\n");

        Assert.True(rules.IsAllowed(Url("/page/1")));
    }

    [Theory]
    [InlineData("/docs/size-guide.pdf", false)]
    [InlineData("/docs/size-guide.pdf?download=1", true)]
    [InlineData("/p/42/print/view", false)]
    [InlineData("/p/42/view", true)]
    [InlineData("/catalog?sort=price&page=2", false)]
    public void Star_and_dollar_wildcards_match(string path, bool allowed)
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /*.pdf$\nDisallow: /*/print/\nDisallow: /*?*sort=\n");

        Assert.Equal(allowed, rules.IsAllowed(Url(path)));
    }

    [Fact]
    public void A_dollar_without_a_star_matches_the_exact_path()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /$\n");

        Assert.False(rules.IsAllowed(Url("/")));
        Assert.True(rules.IsAllowed(Url("/patike")));
    }

    [Fact]
    public void Only_the_star_group_counts()
    {
        var rules = RobotsRules.Parse("User-agent: Googlebot\nDisallow: /\n\nUser-agent: *\nDisallow: /cart\n");

        Assert.True(rules.IsAllowed(Url("/products")));
        Assert.False(rules.IsAllowed(Url("/cart")));
    }

    [Fact]
    public void A_group_shared_by_several_agents_applies_to_star()
    {
        var rules = RobotsRules.Parse("User-agent: bingbot\nUser-agent: *\nDisallow: /tmp\n");

        Assert.False(rules.IsAllowed(Url("/tmp/x")));
    }

    [Fact]
    public void Star_groups_are_merged_and_other_groups_ignored()
    {
        var text = "User-agent: *\nDisallow: /a\n\nUser-agent: other\nDisallow: /b\n\nUser-agent: *\nDisallow: /c\n";
        var rules = RobotsRules.Parse(text);

        Assert.False(rules.IsAllowed(Url("/a")));
        Assert.True(rules.IsAllowed(Url("/b")));
        Assert.False(rules.IsAllowed(Url("/c")));
    }

    [Fact]
    public void An_empty_disallow_allows_everything()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow:\n");

        Assert.Equal(0, rules.Count);
        Assert.True(rules.IsAllowed(Url("/anything")));
    }

    [Fact]
    public void The_robots_file_itself_is_always_allowed()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /\n");

        Assert.True(rules.IsAllowed(Url("/robots.txt")));
        Assert.False(rules.IsAllowed(Url("/patike")));
    }

    [Fact]
    public void Comments_byte_order_mark_and_directive_case_are_ignored()
    {
        var rules = RobotsRules.Parse("\uFEFFuser-AGENT: * # everyone\r\nDISALLOW: /secret # hidden\r\n");

        Assert.False(rules.IsAllowed(Url("/secret/1")));
        Assert.True(rules.IsAllowed(Url("/public")));
    }

    [Fact]
    public void Non_ascii_patterns_match_the_encoded_path()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /патики\n");

        Assert.False(rules.IsAllowed(Url("/патики/nike")));
        Assert.True(rules.IsAllowed(Url("/patike/nike")));
    }

    [Fact]
    public void Allow_all_allows_everything()
    {
        Assert.True(RobotsRules.AllowAll.IsAllowed(Url("/private")));
    }

    private static Uri Url(string path)
    {
        return new Uri("https://shop.test" + path);
    }
}
