using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Collector.Providers.SportM;

internal static partial class SportMPrice
{
    public static decimal? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var kept = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c) || c == '.' || c == ',') kept.Append(c);
        }

        var value = kept.ToString().Trim('.', ',');
        if (value.Length == 0) return null;

        if (value.Contains(','))
        {
            value = value.Replace(".", "").Replace(',', '.');
        }
        else if (ThousandsForm().IsMatch(value))
        {
            value = value.Replace(".", "");
        }

        var culture = CultureInfo.InvariantCulture;
        var parsed = decimal.TryParse(value, NumberStyles.AllowDecimalPoint, culture, out var price);
        return parsed ? price : null;
    }

    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+$")]
    private static partial Regex ThousandsForm();
}
