using System.IO.Compression;
using System.Security.Cryptography;

namespace Collector.Lake;

internal static class LakeBody
{
    private const string TempExtension = ".tmp";
    private const int WindowsFileExists = unchecked((int)0x80070050);
    private const int WindowsAlreadyExists = unchecked((int)0x800700B7);
    private const int UnixFileExists = 17;

    private static readonly FileStreamOptions WriteOptions = new()
    {
        Mode = FileMode.CreateNew,
        Access = FileAccess.Write,
        Share = FileShare.None,
        Options = FileOptions.Asynchronous,
    };

    private static readonly FileStreamOptions ReadOptions = new()
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.Read | FileShare.Delete,
        Options = FileOptions.Asynchronous,
    };

    public static string Hash(byte[] body) => Convert.ToHexStringLower(SHA256.HashData(body));

    public static byte[] Compress(byte[] body)
    {
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(body);
        }

        return compressed.ToArray();
    }

    public static async Task<string> WriteTempAsync(string folder, byte[] compressed, CancellationToken token)
    {
        Directory.CreateDirectory(folder);
        var temp = Path.Combine(folder, $".{Guid.NewGuid():N}{TempExtension}");
        try
        {
            await using var file = new FileStream(temp, WriteOptions);
            await file.WriteAsync(compressed, token);
            file.Flush(flushToDisk: true);
            return temp;
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    public static bool TryMoveIntoPlace(string temp, string path)
    {
        try
        {
            File.Move(temp, path, overwrite: false);
            return true;
        }
        catch (IOException exception) when (exception is not FileNotFoundException && IsTaken(exception, path))
        {
            return false;
        }
    }

    public static async Task<byte[]?> TryReadAsync(string path, CancellationToken token)
    {
        try
        {
            await using var file = new FileStream(path, ReadOptions);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var body = new MemoryStream();
            await gzip.CopyToAsync(body, token);
            return body.ToArray();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsTaken(IOException exception, string path)
    {
        if (exception.HResult is WindowsFileExists or WindowsAlreadyExists) return true;
        if (!OperatingSystem.IsWindows() && exception.HResult == UnixFileExists) return true;
        return File.Exists(path);
    }
}
