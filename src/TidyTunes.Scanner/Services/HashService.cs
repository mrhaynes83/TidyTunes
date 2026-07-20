using System.Security.Cryptography;

namespace TidyTunes.Scanner.Services;

public class HashService
{
    public string CalculateMd5(string filePath)
    {
        using var stream = File.OpenRead(filePath);

        using var md5 = MD5.Create();

        var hash = md5.ComputeHash(stream);

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }


    public string CalculateSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);

        using var sha256 = SHA256.Create();

        var hash = sha256.ComputeHash(stream);

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }


    public (string Md5, string Sha256) CalculateBoth(string filePath)
    {
        // Single pass over the file: both hashes get updated from the
        // same buffer as it streams past, instead of reading the whole
        // file twice (once per algorithm). Roughly halves the I/O time
        // for large libraries.

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