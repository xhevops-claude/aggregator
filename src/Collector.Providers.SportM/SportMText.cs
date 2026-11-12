using System.Text.RegularExpressions;

namespace Collector.Providers.SportM;

internal static partial class SportMText
{
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
