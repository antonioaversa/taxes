using System.Security.Cryptography;

namespace Taxes;

internal static class FileUtils
{
    public static string CalculateMD5Digest(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
