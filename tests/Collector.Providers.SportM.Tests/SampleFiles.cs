using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace Collector.Providers.SportM.Tests;

internal static class SampleFiles
{
    public const string SportMMenShoesPage1 = "sportm-catalog-men-shoes-page-1";
    public const string SportMMenShoesNikePage1 = "sportm-catalog-men-shoes-nike-page-1";
    public const string SportMMenShoesPage400 = "sportm-catalog-men-shoes-page-400";
    public const string SportMGroupOnlyMixedPage1 = "sportm-catalog-grupa01-mixed-page-1";
    public const string IntersportMenShoesPage1 = "intersport-catalog-men-shoes-page-1";
    public const string IntersportMenShoesPage33 = "intersport-catalog-men-shoes-page-33";
    public const string IntersportSaleSneakersPage1 = "intersport-catalog-sale-sneakers-page-1";
    public const string SportMProductIH7234 = "sportm-product-IH7234";
    public const string SportMProductIB2750 = "sportm-product-IB2750-001";
    public const string SportMProductSocks = "sportm-product-KQ6776";
    public const string SportMProductSocksShoeContext = "sportm-product-KQ6776-shoe-context";
    public const string IntersportProductSocksShoeContext = "intersport-product-1304-BLK-shoe-context";
    public const string SportMProductMissing = "sportm-product-missing-ZZ0000000";
    public const string IntersportProductJQ6920 = "intersport-product-JQ6920";
    public const string IntersportProductSkechers = "intersport-product-400603N-BKYL";
    public const string SportMProductEmptyBreadcrumb = "sportm-product-HQ8708-empty-breadcrumb";

    public static byte[] Bytes(string name)
    {
        var path = Path.Combine(Folder(), name + ".html.gz");
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var body = new MemoryStream();
        gzip.CopyTo(body);
        return body.ToArray();
    }

    public static string Text(string name)
    {
        return Encoding.UTF8.GetString(Bytes(name));
    }

    private static string Folder([CallerFilePath] string sourceFile = "")
    {
        return Path.Combine(Path.GetDirectoryName(sourceFile)!, "Samples");
    }
}
