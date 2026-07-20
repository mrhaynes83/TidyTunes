using System.Security.Cryptography;

namespace TidyTunes.Scanner.Services;

public class FileHashService
{
    public string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);

        using var sha256 = SHA256.Create();

        var hash = sha256.ComputeHash(stream);

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }


    public string ComputeMd5(string filePath)
    {
        using var stream = File.OpenRead(filePath);

        using var md5 = MD5.Create();

        var hash = md5.ComputeHash(stream);

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }


    // Computes both hashes from a single read of the file instead of
    // reading it twice (once per algorithm).
    public (string Md5, string Sha256) ComputeBoth(string filePath)
    {
        using var stream = File.OpenRead(filePath);

        using var md5 = MD5.Create();
        using var sha256 = SHA256.Create();

        var buffer = new byte[1024 * 1024];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            md5.TransformBlock(buffer, 0, bytesRead, null, 0);
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        return (
            Convert.ToHexString(md5.Hash!).ToLowerInvariant(),
            Convert.ToHexString(sha256.Hash!).ToLowerInvariant()
        );
    }
}