using System.IO.Compression;
using System.Security.Cryptography;

namespace Collector.Lake.Tests;

public static class Gzip
{
    public static byte[] Compress(byte[] body)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(body);
        }

        return compressed.ToArray();
    }

    public static byte[] Decompress(byte[] compressed)
    {
        using var gzip = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var body = new MemoryStream();
        gzip.CopyTo(body);
        return body.ToArray();
    }

    public static string Hash(byte[] body) => Convert.ToHexStringLower(SHA256.HashData(body));
}
