using System.Xml.Linq;

namespace Collector.Providers.Nbshop;

public static class NbshopSitemap
{
    private static readonly XNamespace Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static List<NbshopProductUrl> ProductUrls(string xml)
    {
        var document = XDocument.Parse(xml);
        var urls = new List<NbshopProductUrl>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var location in document.Descendants(Namespace + "loc"))
        {
            var text = location.Value.Trim();
            if (!NbshopProductUrl.TryParse(text, out var url)) continue;
            if (seen.Add(url.ProviderId)) urls.Add(url);
        }

        return urls;
    }
}
