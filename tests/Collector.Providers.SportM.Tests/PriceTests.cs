using System.Globalization;

namespace Collector.Providers.SportM.Tests;

public sealed class PriceTests
{
    [Theory]
    [InlineData("12.399,00 ден.", "12399")]
    [InlineData(" 799,00 ден.", "799")]
    [InlineData("  2.239,00 ден.", "2239")]
    [InlineData("6099", "6099")]
    [InlineData("1.234", "1234")]
    [InlineData("1.234.567", "1234567")]
    [InlineData("12.5", "12.5")]
    [InlineData("3.599,50", "3599.5")]
    public void ParsesThePriceFormats(string text, string expected)
    {
        var price = decimal.Parse(expected, CultureInfo.InvariantCulture);

        Assert.Equal(price, SportMPrice.Parse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ден.")]
    [InlineData("  ")]
    public void NoAmountIsNull(string? text)
    {
        Assert.Null(SportMPrice.Parse(text));
    }
}
