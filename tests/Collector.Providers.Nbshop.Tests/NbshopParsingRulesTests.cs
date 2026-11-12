using System.Text.Json;

namespace Collector.Providers.Nbshop.Tests;

public class NbshopParsingRulesTests
{
    [Theory]
    [InlineData("5.790", 5790)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("4990.00", 4990)]
    [InlineData(" 3995.50 ", 3995.5)]
    [InlineData("1,234.50", 1234.5)]
    [InlineData("758", 758)]
    public void Prices_follow_the_research_formats(string text, double expected)
    {
        Assert.Equal((decimal)expected, NbshopPrice.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("MKD")]
    [InlineData("-5")]
    public void Unreadable_prices_are_null(string? text)
    {
        Assert.Null(NbshopPrice.Parse(text));
    }

    [Fact]
    public void Json_number_prices_are_read_as_they_are()
    {
        using var json = JsonDocument.Parse("""{"price": 2794.00}""");

        Assert.Equal(2794.00m, NbshopPrice.Parse(json.RootElement.GetProperty("price")));
    }

    [Theory]
    [InlineData("New Balance", "new-balance")]
    [InlineData("adidas", "adidas")]
    [InlineData("  The North Face ", "the-north-face")]
    [InlineData("Under Armour (UA)", "under-armour-ua")]
    [InlineData("Dr. Martens", "dr-martens")]
    public void Brand_slugs_follow_the_research_rule(string brand, string slug)
    {
        Assert.Equal(slug, NbshopListingPath.BrandSlug(brand));
    }

    [Fact]
    public void A_brand_listing_is_under_proizvodi()
    {
        var listing = NbshopListingPath.ForBrand("New Balance");

        Assert.NotNull(listing);
        var first = listing.PageUrl("https://www.sportvision.mk/", "mk", 1);
        var third = listing.PageUrl("https://www.sportvision.mk", "mk", 3);
        Assert.Equal("https://www.sportvision.mk/mk/proizvodi/new-balance", first);
        Assert.Equal("https://www.sportvision.mk/mk/proizvodi/new-balance/page-3", third);
        Assert.Equal("proizvodi/new-balance/page-3", listing.ProviderId(3));
    }

    [Fact]
    public void Product_urls_split_into_language_category_id_and_slug()
    {
        var url = "https://www.sportvision.mk/mk/patiki-za-voda/8582940-speedo-surfknit-pro-watershoe";

        Assert.True(NbshopProductUrl.TryParse(url, out var parsed));

        Assert.Equal("mk", parsed.Language);
        Assert.Equal("patiki-za-voda", parsed.Category);
        Assert.Equal("8582940", parsed.ProviderId);
        Assert.Equal("speedo-surfknit-pro-watershoe", parsed.Slug);
        Assert.Equal("speedo surfknit pro watershoe", parsed.TitleFromSlug);
        Assert.Equal(url, parsed.Url);
    }

    [Fact]
    public void Old_products_keep_short_numeric_ids()
    {
        var url = "https://www.buzzsneakers.mk/mk/patiki/108-adidas-patiki-gazelle";

        Assert.True(NbshopProductUrl.TryParse(url, out var parsed));

        Assert.Equal("108", parsed.ProviderId);
        Assert.Equal("patiki", parsed.Category);
    }

    [Theory]
    [InlineData("https://www.sportvision.mk/mk/patiki")]
    [InlineData("https://www.sportvision.mk/mk/proizvodi/converse/page-2")]
    [InlineData("/mk/patiki/8426431-harden-stepback")]
    [InlineData("not a url")]
    public void Other_urls_are_not_product_urls(string url)
    {
        Assert.False(NbshopProductUrl.TryParse(url, out _));
    }

    [Fact]
    public void Listing_tiles_need_a_five_digit_id_and_page_links_are_collected()
    {
        var html = """
            <a href="https://www.sportvision.mk/mk/akcii/255-akcija-1120" title="Акција">Акција</a>
            <a href="https://www.sportvision.mk/mk/prodavnici/16-sport-vision-12-kumanovo" title="Куманово">x</a>
            <a href="/mk/patiki/8426431-harden-stepback"><img></a>
            <a href="/mk/patiki/8426431-harden-stepback" title=" adidas Harden Stepback ">adidas</a>
            <a href="/mk/patiki/8426432-harden-stepback" title="adidas Harden Stepback 2">adidas</a>
            <a rel="next" href="/mk/proizvodi/adidas/page-2">next</a>
            <a href="/mk/proizvodi/?outlet=yes/page-7">outlet</a>
            """;

        var page = NbshopListingPage.Parse(html, new Uri("https://www.sportvision.mk/mk/proizvodi/adidas"));

        Assert.Equal(["8426431", "8426432"], page.Tiles.Select(tile => tile.ProviderId));
        Assert.Equal("adidas Harden Stepback", page.Tiles[0].Title);
        Assert.Equal("https://www.sportvision.mk/mk/patiki/8426431-harden-stepback", page.Tiles[0].Url);
        Assert.Equal("patiki", page.Tiles[0].Category);
        Assert.True(page.LinksToPage(2));
        Assert.True(page.LinksToPage(7));
        Assert.False(page.LinksToPage(3));
    }

    [Theory]
    [InlineData("patiki", true)]
    [InlineData("papuci", true)]
    [InlineData("kopacki", true)]
    [InlineData("konduri", true)]
    [InlineData("baletanki", true)]
    [InlineData("patiki-za-voda", true)]
    [InlineData("vloski", false)]
    [InlineData("vrvci", false)]
    [InlineData("corapi", false)]
    [InlineData("dukser", false)]
    [InlineData(null, false)]
    public void The_shoe_category_list_decides_the_apples(string? slug, bool isShoe)
    {
        Assert.Equal(isShoe, NbshopShoeCategories.Contains(slug));
    }

    [Fact]
    public void Control_characters_inside_json_strings_are_escaped_and_the_rest_is_kept()
    {
        var raw = "{\n  \"name\": \"line one\nline two\ttab\",\n  \"quote\": \"say \\\"hi\\\"\r\n\"\n}";
        var expected = "{\n  \"name\": \"line one\\nline two\\ttab\",\n  \"quote\": \"say \\\"hi\\\"\\r\\n\"\n}";

        var sanitized = NbshopJsonLd.Sanitize(raw);

        Assert.Equal(expected, sanitized);
        using var json = JsonDocument.Parse(sanitized);
        Assert.Equal("line one\nline two\ttab", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("say \"hi\"\r\n", json.RootElement.GetProperty("quote").GetString());
    }

    [Fact]
    public void Other_control_characters_become_unicode_escapes()
    {
        Assert.Equal("\"a\\u0001b\"", NbshopJsonLd.Sanitize("\"a\u0001b\""));
    }

    [Fact]
    public void A_block_that_is_not_json_is_skipped()
    {
        Assert.Null(NbshopJsonLd.TryParse("--><!--\"name\": \"--><!--\","));
        Assert.NotNull(NbshopJsonLd.TryParse("{\"@type\": \"Product\",}"));
    }
}
