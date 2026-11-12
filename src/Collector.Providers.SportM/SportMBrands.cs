namespace Collector.Providers.SportM;

internal static class SportMBrands
{
    private static readonly (string Name, string Code)[] Coded =
    [
        ("adidas", "01"),
        ("Nike", "02"),
        ("Puma", "04"),
        ("Reebok", "07"),
        ("4F", "69"),
        ("Skechers", "70"),
        ("Asics", "72"),
        ("Salomon", "73"),
        ("New Balance", "81"),
        ("Energetics", "100"),
        ("McKinley", "104"),
        ("On", "122"),
        ("Crocs", "00050"),
        ("Jack Wolfskin", "00071"),
        ("Salewa", "00074"),
    ];

    private static readonly string[] MultiWord =
        ["New Balance", "Jack Wolfskin", "The North Face", "Under Armour", "Dare 2b", "D.Franklin"];

    public static string? CodeFor(string brand)
    {
        var value = brand.Trim();
        foreach (var (name, code) in Coded)
        {
            if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) return code;
        }

        if (value.Length > 0 && value.All(char.IsAsciiDigit)) return value;
        return null;
    }

    public static string? NameForCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        foreach (var (name, known) in Coded)
        {
            if (known == code.Trim()) return name;
        }

        return null;
    }

    public static string FromName(string productName)
    {
        var name = productName.Trim();
        foreach (var brand in MultiWord)
        {
            if (name.StartsWith(brand + " ", StringComparison.OrdinalIgnoreCase)) return brand;
        }

        var space = name.IndexOf(' ');
        return space < 0 ? name : name[..space];
    }

    public static string Canonical(string brand)
    {
        var value = brand.Trim();
        foreach (var (name, _) in Coded)
        {
            if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) return name;
        }

        foreach (var name in MultiWord)
        {
            if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) return name;
        }

        return value;
    }
}
