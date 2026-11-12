using System.Text;
using Collector.Core.Domains;
using Collector.Core.Entities;
using Collector.Core.Ids;
using Collector.Core.Platform;

namespace Collector.Providers.Nbshop.Tests;

public class NbshopShoesAdapterProductPageTests
{
    private const string ConverseUrl =
        "https://www.sportvision.mk/mk/patiki/8733931-converse-chuck-taylor-all-star-equip";
    private const string SportVisionVsPaceUrl = "https://www.sportvision.mk/mk/patiki/8566650-adidas-vs-pace-2-0";
    private const string SportRealityVsPaceUrl = "https://www.sportreality.mk/mk/patiki/181427-adidas-vs-pace-2-0";
    private const string CrocsUrl = "https://www.buzzsneakers.mk/mk/papuci/236427-crocs-papuci-classic";
    private const string HoodieUrl = "https://www.sportreality.mk/mk/dukser/181711-under-armour-icon-fleece-hoodie";

    private static readonly Guid Seed = TestShops.ProjectSeed;

    private readonly NbshopShoesAdapter adapter = new();

    [Fact]
    public void A_discounted_shoe_gives_product_listing_and_category_with_exact_values()
    {
        var entities = Adapt(TestShops.SportVision, ConverseUrl, SampleFiles.SportVisionConverseShoe);

        Assert.Equal(3, entities.Count);
        var product = Assert.IsType<Product>(entities[0]);
        var listing = Assert.IsType<Listing>(entities[1]);
        var category = Assert.IsType<Category>(entities[2]);

        var productId = ProductIdentity.Candidate(Seed, "sportvision", "8733931");
        var categoryId = DeterministicId.For(Seed, "sportvision", "category", "patiki");
        var images = new List<string>
        {
            "https://www.sportvision.mk/files/images/slike_proizvoda/media/A08/A08556C/images/A08556C.jpg",
            "https://www.sportvision.mk/files/images/slike_proizvoda/media/A08/A08556C/images/A08556C_1.jpg",
            "https://www.sportvision.mk/files/images/slike_proizvoda/media/A08/A08556C/images/A08556C_2.jpg",
        };
        var expectedProduct = new Product
        {
            Id = productId,
            ProjectId = TestShops.ProjectId,
            Namespace = "clothing/shoes/sportvision",
            Provider = "sportvision",
            ProviderId = "8733931",
            SourceUrl = ConverseUrl,
            HarvestedAt = TestShops.HarvestedAt,
            Name = "Converse Chuck Taylor All Star Equip",
            Brand = "Converse",
            Sku = "A08556C",
            CategoryId = categoryId,
            Fields = new ShoeFields(),
        };
        Assert.Equal(images, product.Images);
        Assert.Empty(product.Aliases);
        var comparable = product with { Images = expectedProduct.Images, Aliases = expectedProduct.Aliases };
        Assert.Equal(expectedProduct, comparable);

        var expectedListing = new Listing
        {
            Id = DeterministicId.For(Seed, "sportvision", "listing", "8733931"),
            ProjectId = TestShops.ProjectId,
            Namespace = "clothing/shoes/sportvision",
            Provider = "sportvision",
            ProviderId = "8733931",
            SourceUrl = ConverseUrl,
            HarvestedAt = TestShops.HarvestedAt,
            ProductId = productId,
            Title = "Converse Chuck Taylor All Star Equip",
            Description = listing.Description,
            Price = 3995m,
            OldPrice = 7990m,
            Currency = "MKD",
            Availability = Availability.InStock,
            Url = ConverseUrl,
            InfoCheckedAt = TestShops.HarvestedAt,
            StockCheckedAt = TestShops.HarvestedAt,
        };
        Assert.Equal(expectedListing, listing with { Sizes = expectedListing.Sizes });
        Assert.StartsWith("Converse Chuck Taylor All Star Equip унисекс lifestyle патики", listing.Description);
        Assert.EndsWith("врвки за брзо врзување", listing.Description);

        string[] labels =
        [
            "36", "37", "37.5", "38", "38.5", "39", "40", "40.5", "41", "42", "42.5", "43", "44", "44.5", "45", "46",
        ];
        Assert.Equal(labels, listing.Sizes.Select(size => size.Label));
        Assert.All(listing.Sizes, size => Assert.Equal("EU", size.System));
        Assert.All(listing.Sizes, size => Assert.Null(size.Branch));
        var inStock = Assert.Single(listing.Sizes, size => size.InStock == true);
        Assert.Equal(new Size { Label = "42.5", System = "EU", Ean = "194435095288", InStock = true }, inStock);
        Assert.All(listing.Sizes.Where(size => size != inStock), size => Assert.Null(size.Ean));

        var expectedCategory = new Category
        {
            Id = categoryId,
            ProjectId = TestShops.ProjectId,
            Namespace = "clothing/shoes/sportvision",
            Provider = "sportvision",
            ProviderId = "patiki",
            SourceUrl = ConverseUrl,
            HarvestedAt = TestShops.HarvestedAt,
            Name = "Патики",
            Path = "patiki",
        };
        Assert.Equal(expectedCategory, category);
    }

    [Fact]
    public void A_re_check_of_an_unchanged_page_moves_both_listing_dates_and_the_observation_time()
    {
        var checkedAt = TestShops.HarvestedAt.AddDays(3);
        var source = TestShops.SportVision;
        var raw = TestShops.Raw(source, "product-page", "8733931", ConverseUrl, SampleFiles.SportVisionConverseShoe);
        var rechecked = raw with { Item = raw.Item with { CheckedAt = checkedAt } };

        var entities = adapter.Adapt(rechecked, TestShops.Context(source)).ToList();

        var listing = entities.OfType<Listing>().Single();
        Assert.Equal(checkedAt, listing.InfoCheckedAt);
        Assert.Equal(checkedAt, listing.StockCheckedAt);
        Assert.All(entities, entity => Assert.Equal(checkedAt, entity.HarvestedAt));
    }

    [Fact]
    public void A_sport_vision_page_with_a_manufacturer_code_fills_the_model_code_and_size_eans()
    {
        var entities = Adapt(TestShops.SportVision, SportVisionVsPaceUrl, SampleFiles.SportVisionVsPace);

        var product = entities.OfType<Product>().Single();
        var listing = entities.OfType<Listing>().Single();
        Assert.Equal("adidas VS PACE 2.0", product.Name);
        Assert.Equal("adidas", product.Brand);
        Assert.Equal("HP6005", product.Sku);
        Assert.Equal(new ShoeFields { ModelCode = "LSH59" }, product.Fields);
        Assert.Equal(5, product.Images.Count);
        Assert.Equal(2794m, listing.Price);
        Assert.Equal(3288m, listing.OldPrice);
        Assert.Equal(24, listing.Sizes.Count);
        Assert.Equal("50 2/3", listing.Sizes[0].Label);
        Assert.Equal(false, listing.Sizes[0].InStock);

        var inStock = listing.Sizes.Where(size => size.InStock == true).Select(size => (size.Label, size.Ean));
        (string, string?)[] expected =
        [
            ("41 1/3", "4066748660742"), ("42", "4066748660643"), ("42 2/3", "4066748660636"),
            ("43 1/3", "4066748660674"), ("44", "4066748660735"), ("44 2/3", "4066748660698"),
            ("45 1/3", "4066748660681"), ("46", "4066748660773"),
        ];
        Assert.Equal(expected, inStock);
    }

    [Fact]
    public void A_sport_reality_page_without_discount_has_no_old_price_and_reads_the_serbian_code_label()
    {
        var entities = Adapt(TestShops.SportReality, SportRealityVsPaceUrl, SampleFiles.SportRealityVsPace);

        var product = entities.OfType<Product>().Single();
        var listing = entities.OfType<Listing>().Single();
        var category = entities.OfType<Category>().Single();
        Assert.Equal(ProductIdentity.Candidate(Seed, "sportreality", "181427"), product.Id);
        Assert.Equal("181427", product.ProviderId);
        Assert.Equal("HP6005", product.Sku);
        Assert.Equal(new ShoeFields { ModelCode = "LSH59" }, product.Fields);
        Assert.Equal(3288m, listing.Price);
        Assert.Null(listing.OldPrice);
        Assert.Equal("MKD", listing.Currency);
        Assert.Equal(Availability.InStock, listing.Availability);
        Assert.Equal(20, listing.Sizes.Count);
        Assert.Equal(10, listing.Sizes.Count(size => size.InStock == true));
        Assert.Equal(DeterministicId.For(Seed, "sportreality", "category", "patiki"), category.Id);
        Assert.Equal("Патики", category.Name);
    }

    [Fact]
    public void A_buzz_page_with_raw_tabs_in_the_json_ld_still_parses()
    {
        var entities = Adapt(TestShops.Buzz, CrocsUrl, SampleFiles.BuzzCrocs);

        var product = entities.OfType<Product>().Single();
        var listing = entities.OfType<Listing>().Single();
        var category = entities.OfType<Category>().Single();
        Assert.Equal("Crocs Папучи Classic", product.Name);
        Assert.Equal("Crocs", product.Brand);
        Assert.Equal("10001-100", product.Sku);
        Assert.Equal(new ShoeFields(), product.Fields);
        Assert.Equal(3032m, listing.Price);
        Assert.Equal(3790m, listing.OldPrice);
        Assert.StartsWith("Crocs CLASSIC се унисекс папучи", listing.Description);
        Assert.EndsWith("Спецификација:  \tГорниште со дупчиња   \tГумен материјал", listing.Description);
        Assert.Equal("Папучи", category.Name);
        Assert.Equal("papuci", category.Path);

        Assert.Equal(18, listing.Sizes.Count);
        var inStock = Assert.Single(listing.Sizes, size => size.InStock == true);
        Assert.Equal(new Size { Label = "39-40", System = "EU", Ean = "883503185630", InStock = true }, inStock);
        var noEuSize = listing.Sizes.Single(size => size.Label == "M14");
        Assert.Null(noEuSize.System);
    }

    [Fact]
    public void A_product_outside_the_shoe_categories_gives_nothing()
    {
        var entities = Adapt(TestShops.SportReality, HoodieUrl, SampleFiles.SportRealityHoodie);

        Assert.Empty(entities);
    }

    [Fact]
    public void Raw_newlines_in_json_ld_strings_and_missing_fields_fall_back_as_the_research_says()
    {
        const string url = "https://www.sportvision.mk/mk/cizmi/8700001-test-boot";
        var json = """
            {"@type":"Product","name":"Test<NL>Boot","brand":"Kronos",
            "image":"https://www.sportvision.mk/a.jpg",
            "offers":{"price":"1.290","availability":"https://schema.org/OutOfStock"}}
            """.ReplaceLineEndings("").Replace("<NL>", "\n");
        var body = """<div class="category"><a href="/mk/cizmi" title="Чизми">Чизми</a></div>""";

        var entities = AdaptHtml(url, Page(json, body));

        var product = entities.OfType<Product>().Single();
        var listing = entities.OfType<Listing>().Single();
        Assert.Equal("8700001", product.ProviderId);
        Assert.Equal("Test\nBoot", product.Name);
        Assert.Equal("Kronos", product.Brand);
        Assert.Null(product.Sku);
        Assert.Equal(["https://www.sportvision.mk/a.jpg"], product.Images);
        Assert.Equal(1290m, listing.Price);
        Assert.Equal("MKD", listing.Currency);
        Assert.Equal(Availability.OutOfStock, listing.Availability);
        Assert.Empty(listing.Sizes);
        Assert.Equal("Чизми", entities.OfType<Category>().Single().Name);
    }

    [Fact]
    public void A_shoe_page_without_a_product_block_is_a_per_item_failure()
    {
        const string url = "https://www.sportvision.mk/mk/patiki/8700002-no-data";
        var html = Page("""{"@type":"BreadcrumbList"}""", "");

        Assert.Throws<InvalidDataException>(() => AdaptHtml(url, html));
    }

    [Fact]
    public void A_page_that_is_not_a_product_url_is_a_per_item_failure()
    {
        const string url = "https://www.sportvision.mk/mk/patiki";

        Assert.Throws<InvalidDataException>(() => AdaptHtml(url, "<html></html>"));
    }

    private List<Entity> Adapt(Source source, string url, string sample)
    {
        var raw = TestShops.Raw(source, "product-page", ProviderIdOf(url), url, sample);
        return adapter.Adapt(raw, TestShops.Context(source)).ToList();
    }

    private List<Entity> AdaptHtml(string url, string html)
    {
        var source = TestShops.SportVision;
        var raw = TestShops.Raw(source, "product-page", "test", url, SampleFiles.SportVisionConverseShoe);
        var page = raw with { Body = Encoding.UTF8.GetBytes(html) };
        return adapter.Adapt(page, TestShops.Context(source)).ToList();
    }

    private static string Page(string json, string body) =>
        $"<html><head><script type=\"application/ld+json\">{json}</script></head><body>{body}</body></html>";

    private static string ProviderIdOf(string url)
    {
        Assert.True(NbshopProductUrl.TryParse(url, out var parsed));
        return parsed.ProviderId;
    }
}
