using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Collector.Core.Lake;

namespace Collector.Lake;

public static partial class LakePath
{
    public const int ReadablePrefixLength = 40;
    public const int HashSuffixLength = 16;
    public const int PlainNameLength = 64;
    public const string BodyExtension = ".gz";

    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

    public static string Folder(LakeKey key)
    {
        var source = NameSegment(key.Source);
        var kind = NameSegment(key.Kind);
        var providerId = HashSuffixedSegment(key.ProviderId);
        return $"{key.ProjectId}/{source}/{kind}/{providerId}";
    }

    public static string FileName(int version) => $"{version:D6}{BodyExtension}";

    public static string Location(LakeKey key, int version) => $"{Folder(key)}/{FileName(version)}";

    public static string NameSegment(string name)
    {
        if (IsPlainName(name)) return name;
        return HashSuffixedSegment(name);
    }

    public static string HashSuffixedSegment(string value)
    {
        var hash = HashSuffix(value);
        var readable = ReadablePrefix(value);
        if (readable.Length == 0) return hash;
        return $"{readable}-{hash}";
    }

    private static bool IsPlainName(string name)
    {
        if (name.Length > PlainNameLength) return false;
        if (!PlainName().IsMatch(name)) return false;
        return !IsDeviceName(name);
    }

    private static bool IsDeviceName(string name)
    {
        var stem = name.Split('.')[0];
        if (stem is "con" or "prn" or "aux" or "nul") return true;
        if (stem.Length != 4 || !char.IsAsciiDigit(stem[3])) return false;
        return stem.StartsWith("com", StringComparison.Ordinal) || stem.StartsWith("lpt", StringComparison.Ordinal);
    }

    private static string ReadablePrefix(string value)
    {
        var builder = new StringBuilder(ReadablePrefixLength);
        foreach (var character in value)
        {
            if (builder.Length == ReadablePrefixLength) break;
            var safe = char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
            builder.Append(safe ? character : '_');
        }

        return builder.ToString().Trim('_', '-');
    }

    private static string HashSuffix(string value)
    {
        var hash = SHA256.HashData(StrictUtf8.GetBytes(value));
        return Convert.ToHexStringLower(hash)[..HashSuffixLength];
    }

    [GeneratedRegex("^[a-z0-9]+(?:[._-][a-z0-9]+)*$")]
    private static partial Regex PlainName();
}
