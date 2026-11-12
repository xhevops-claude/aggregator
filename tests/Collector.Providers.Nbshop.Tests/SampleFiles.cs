using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace Collector.Providers.Nbshop.Tests;

public static class SampleFiles
{
    public const string SportVisionSitemap = "sportvision-product-sitemap.xml";
    public const string SportVisionConversePage1 = "sportvision-listing-converse-page-1.html";
    public const string SportVisionConversePage4 = "sportvision-listing-converse-page-4.html";
    public const string BuzzConversePage2 = "buzzsneakers-listing-converse-page-2.html";
    public const string SportRealityOutletPage1 = "sportreality-listing-outlet-page-1.html";
    public const string SportVisionConverseShoe = "sportvision-product-8733931.html";
    public const string SportVisionVsPace = "sportvision-product-8566650.html";
    public const string SportRealityVsPace = "sportreality-product-181427.html";
    public const string BuzzCrocs = "buzzsneakers-product-236427.html";
    public const string SportRealityHoodie = "sportreality-product-181711.html";

    public static byte[] Bytes(string name)
    {
        var path = Path.Combine(Folder(), "Samples", name + ".gz");
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var memory = new MemoryStream();
        gzip.CopyTo(memory);
        return memory.ToArray();
    }

    public static string Text(string name) => Encoding.UTF8.GetString(Bytes(name));

    private static string Folder([CallerFilePath] string path = "") => Path.GetDirectoryName(path) ?? "";
}
