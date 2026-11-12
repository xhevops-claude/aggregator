using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace Collector.Providers.Nbshop;

public sealed partial record NbshopListingPage
{
    public const int MinimumTileIdDigits = 5;

    public List<NbshopTile> Tiles { get; init; } = [];
    public HashSet<int> LinkedPages { get; init; } = [];

    public bool LinksToPage(int page) => LinkedPages.Contains(page);

    public static NbshopListingPage Parse(string html, Uri pageUrl)
    {
        var document = new HtmlParser().ParseDocument(html);
        var tiles = new List<NbshopTile>();
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var linkedPages = new HashSet<int>();

        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            if (!Uri.TryCreate(pageUrl, anchor.GetAttribute("href"), out var url)) continue;

            var linkedPage = LinkedPage(url);
            if (linkedPage is not null) linkedPages.Add(linkedPage.Value);

            if (!NbshopProductUrl.TryParse(url.ToString(), out var product)) continue;
            if (product.ProviderId.Length < MinimumTileIdDigits) continue;

            var title = anchor.GetAttribute("title")?.Trim() ?? "";
            if (positions.TryGetValue(product.ProviderId, out var position))
            {
                if (tiles[position].Title.Length == 0) tiles[position] = tiles[position] with { Title = title };
                continue;
            }

            positions[product.ProviderId] = tiles.Count;
            tiles.Add(new NbshopTile
            {
                ProviderId = product.ProviderId,
                Category = product.Category,
                Url = product.Url,
                Title = title,
            });
        }

        return new NbshopListingPage { Tiles = tiles, LinkedPages = linkedPages };
    }

    private static int? LinkedPage(Uri url)
    {
        var match = PageSegment().Match(url.PathAndQuery);
        if (!match.Success) return null;

        return int.Parse(match.Groups["page"].Value, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"/page-(?<page>\d{1,6})/?(?=$|\?)")]
    private static partial Regex PageSegment();
}
