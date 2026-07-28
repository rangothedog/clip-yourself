using System.Security.Cryptography;
using System.Text;

namespace ClipYourself.Core.Services;

public static class ClipHasher
{
    private const long MaxHashFileBytes = 64L * 1024 * 1024;

    public static string HashText(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static string HashBytes(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes));

    public static string HashBytes(ReadOnlySpan<byte> bytes)
        => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>
    /// Hashes a file's contents. Large files are hashed by identity
    /// (path + length + last-write time) to avoid reading gigabytes.
    /// </summary>
    public static string HashFile(string path)
    {
        var info = new FileInfo(path);
        if (info.Length <= MaxHashFileBytes)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        return HashText($"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
    }

    public static string HashPaths(IEnumerable<string> paths)
        => HashText(string.Join("\n", paths.Select(p => p.ToLowerInvariant())));
}
