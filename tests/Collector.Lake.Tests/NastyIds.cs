namespace Collector.Lake.Tests;

public static class NastyIds
{
    public static IReadOnlyList<string> All { get; } =
    [
        "4711", "HP6005", "hp6005", "Hp6005",
        "a/b", "a\\b", "a:b", "a_b", "a b", "a?b", "a*b", "a|b", "a<b>", "a\"b", "a.b", "a-b",
        "?id=12&colour=white", "https://www.sport-m.com.mk/Product.aspx?id=12&b=01",
        "%2F", "%2f", "/", "//", "\\",
        ".", "..", "...", " ", "  ", "trailing.", "trailing ", " leading", "-", "_",
        "CON", "con", "NUL.txt", "COM1", "LPT9", "aux.tar.gz",
        "Патики-42", "патики-42", "ПАТИКИ-42", "👟", "👟🔥", "\u00e9", "e\u0301",
        "tab\there", "new\nline",
        new string('x', 300), new string('x', 300) + "y", new string('x', 40) + "/1", new string('x', 40) + "/2",
        new string('я', 1000),
    ];

    public static TheoryData<string> Theory()
    {
        var data = new TheoryData<string>();
        foreach (var providerId in All) data.Add(providerId);
        return data;
    }
}
