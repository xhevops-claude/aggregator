namespace Collector.Providers.SportM.Tests;

public sealed class UrlTests
{
    [Theory]
    [InlineData("/ProductImages/IH7234.webp", "https://sport-m.com.mk/ProductImages/IH7234.webp")]
    [InlineData("ProductImages/a b.webp", "https://sport-m.com.mk/ProductImages/a%20b.webp")]
    [InlineData("/ProductImages/x_1-2.webp", "https://sport-m.com.mk/ProductImages/x_1-2.webp")]
    [InlineData("/ProductImages/ж(1).webp", "https://sport-m.com.mk/ProductImages/%D0%B6%281%29.webp")]
    [InlineData("https://cdn.example/a b.webp", "https://cdn.example/a b.webp")]
    public void AbsoluteImageUrls(string path, string expected)
    {
        Assert.Equal(expected, SportMUrls.Absolute(Shops.SportM, path));
    }

    [Fact]
    public void ProductUrlEscapesTheId()
    {
        var url = SportMUrls.ProductPage(Shops.SportM, "A B&C");

        Assert.Equal("https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=A%20B%26C", url);
    }

    [Theory]
    [InlineData("https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=IH7234")]
    [InlineData("https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=IH7234&Segment=03&Grupa=01")]
    [InlineData("https://sport-m.com.mk/ProductCatalog/ProductDetails?Segment=17&Grupa=103&productid=IH7234")]
    [InlineData("https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=IH7234&PodGrupa=514#sizes")]
    public void ProductUrlKeepsOnlyTheProductId(string url)
    {
        var expected = "https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=IH7234";

        Assert.Equal(expected, SportMUrls.WithoutContext(url));
    }

    [Fact]
    public void ProductUrlWithoutContextKeepsTheEscapedId()
    {
        var url = "https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=A%20B%26C&Grupa=01";

        var clean = SportMUrls.WithoutContext(url);

        Assert.Equal("https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=A%20B%26C", clean);
    }

    [Fact]
    public void UrlWithoutProductIdIsKeptAsGiven()
    {
        var url = "https://sport-m.com.mk/ProductCatalog?Segment=03&Grupa=01";

        Assert.Equal(url, SportMUrls.WithoutContext(url));
    }

    [Theory]
    [InlineData("https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=IH7234", false)]
    [InlineData("https://sport-m.com.mk/ProductCatalog/ProductDetails?productid=IH7234#top", false)]
    [InlineData("", false)]
    [InlineData("https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=IH7234&Grupa=01", true)]
    [InlineData("https://sport-m.com.mk/ProductCatalog/ProductDetails?Segment=03&ProductID=IH7234", true)]
    [InlineData("https://sport-m.com.mk/ProductCatalog/ProductDetails?ProductID=IH7234&Pol", true)]
    public void ContextIsAnyParameterButTheProductId(string url, bool expected)
    {
        Assert.Equal(expected, SportMUrls.HasContext(url));
    }

    [Fact]
    public void CatalogUrlAppendsThePage()
    {
        Assert.Equal("https://intersport.mk/ProductCatalog?Page=3", SportMUrls.CatalogPage(Shops.Intersport, "", 3));
        var url = SportMUrls.CatalogPage(Shops.Intersport, "Segment=28&Grupa=01", 2);
        Assert.Equal("https://intersport.mk/ProductCatalog?Segment=28&Grupa=01&Page=2", url);
    }
}
