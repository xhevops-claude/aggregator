namespace Collector.Providers.SportM.Tests;

public sealed class BrandTests
{
    [Theory]
    [InlineData("adidas", "01")]
    [InlineData("ADIDAS", "01")]
    [InlineData("Nike", "02")]
    [InlineData("new balance", "81")]
    [InlineData("Crocs", "00050")]
    [InlineData("132", "132")]
    [InlineData(" 04 ", "04")]
    public void CodeForBrandNameOrCode(string brand, string expected)
    {
        Assert.Equal(expected, SportMBrands.CodeFor(brand));
    }

    [Fact]
    public void UnknownBrandHasNoCode()
    {
        Assert.Null(SportMBrands.CodeFor("Lotto"));
    }

    [Theory]
    [InlineData("02", "Nike")]
    [InlineData("00071", "Jack Wolfskin")]
    [InlineData("2", null)]
    [InlineData(null, null)]
    public void NameForCode(string? code, string? expected)
    {
        Assert.Equal(expected, SportMBrands.NameForCode(code));
    }

    [Theory]
    [InlineData("Adidas HOOPS 4.0", "Adidas")]
    [InlineData("NIKE CHARGE SUEDE", "NIKE")]
    [InlineData("New Balance 574", "New Balance")]
    [InlineData("THE NORTH FACE NUPTSE", "The North Face")]
    [InlineData("Dare 2b Jacket", "Dare 2b")]
    [InlineData("D.Franklin SNEAKER", "D.Franklin")]
    [InlineData("4F CAP", "4F")]
    [InlineData("Newbalance", "Newbalance")]
    public void BrandFromName(string name, string expected)
    {
        Assert.Equal(expected, SportMBrands.FromName(name));
    }

    [Theory]
    [InlineData("Adidas", "adidas")]
    [InlineData("NIKE", "Nike")]
    [InlineData("Mckinley", "McKinley")]
    [InlineData("CROCS", "Crocs")]
    [InlineData("the north face", "The North Face")]
    [InlineData("Terinda", "Terinda")]
    public void CanonicalSpelling(string brand, string expected)
    {
        Assert.Equal(expected, SportMBrands.Canonical(brand));
    }
}
