using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Collector.Providers.Nbshop;

public static partial class NbshopPrice
{
    public static decimal? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var trimmed = text.Trim();
        var normalized = ThousandsForm().IsMatch(trimmed) ? trimmed.Replace(".", "") : trimmed.Replace(",", "");
        var styles = NumberStyles.AllowDecimalPoint;
        if (decimal.TryParse(normalized, styles, CultureInfo.InvariantCulture, out var value)) return value;

        return null;
    }

    public static decimal? Parse(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number)) return number;
        if (element.ValueKind == JsonValueKind.String) return Parse(element.GetString());

        return null;
    }

    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+$")]
    private static partial Regex ThousandsForm();
}
