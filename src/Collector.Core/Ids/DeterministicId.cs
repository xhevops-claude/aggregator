using System.Security.Cryptography;
using System.Text;

namespace Collector.Core.Ids;

public static class DeterministicId
{
    public static Guid Create(Guid seed, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[16 + nameBytes.Length];
        seed.TryWriteBytes(input, bigEndian: true, out _);
        nameBytes.CopyTo(input, 16);

        var bytes = SHA1.HashData(input)[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    public static Guid For(Guid projectSeed, string provider, string kind, string providerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(provider);
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentException.ThrowIfNullOrEmpty(providerId);
        return Create(projectSeed, $"{provider}:{kind}:{providerId}");
    }
}
