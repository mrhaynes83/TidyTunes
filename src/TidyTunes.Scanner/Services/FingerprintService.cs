using System.Diagnostics;
using System.Text.Json;

namespace TidyTunes.Scanner.Services;

public class FingerprintService
{
    private readonly string _fpCalcPath;

    public FingerprintService(string fpCalcPath)
    {
        _fpCalcPath = fpCalcPath;
    }


    // Confirms fpcalc can actually be found and run before starting a
    // long batch job, so failures show up immediately instead of after
    // the first file.
    public bool IsAvailable(out string resolvedPath)
    {
        resolvedPath = ResolvePath();

        return File.Exists(resolvedPath);
    }


    private string ResolvePath()
    {
        if (File.Exists(_fpCalcPath))
        {
            return _fpCalcPath;
        }

        var withExtension = _fpCalcPath + ".exe";

        if (File.Exists(withExtension))
        {
            return withExtension;
        }

        // Return the original path so callers see the path the user
        // configured when reporting "not found" errors.
        return _fpCalcPath;
    }


    public (double Duration, string Fingerprint)? Generate(string filePath)
    {
        var exePath = ResolvePath();

        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(
                $"fpcalc not found at '{_fpCalcPath}'. " +
                "Download it from https://acoustid.org/chromaprint and " +
                "set FpCalcPath in settings.json.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"-json \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();

        process.WaitForExit(30000);

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(output);

        var root = doc.RootElement;

        if (!root.TryGetProperty("fingerprint", out var fingerprintElement) ||
            !root.TryGetProperty("duration", out var durationElement))
        {
            return null;
        }

        var fingerprint = fingerprintElement.GetString();

        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return null;
        }

        var duration = durationElement.GetDouble();

        return (duration, fingerprint);
    }
}
