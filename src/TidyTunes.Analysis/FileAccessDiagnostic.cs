using TidyTunes.Core.Models;

namespace TidyTunes.Analysis;

public class FileAccessDiagnostic
{
    public Dictionary<string, int> CategoryCounts { get; } = new();

    public Dictionary<string, List<string>> Samples { get; } = new();

    private const int MaxSamplesPerCategory = 5;


    // Fast (no hashing/fingerprinting) pass over a set of files to find
    // out WHY they're inaccessible, instead of guessing. This is meant
    // to run in seconds/minutes even over tens of thousands of files,
    // since it only touches file metadata and a single byte of content.
    public void Diagnose(List<LibraryFile> files)
    {
        foreach (var file in files)
        {
            var category = ClassifyFile(file.FilePath);

            CategoryCounts[category] = CategoryCounts.GetValueOrDefault(category) + 1;

            if (!Samples.TryGetValue(category, out var sampleList))
            {
                sampleList = new List<string>();
                Samples[category] = sampleList;
            }

            if (sampleList.Count < MaxSamplesPerCategory)
            {
                sampleList.Add(file.FilePath);
            }
        }
    }


    private static string ClassifyFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "EmptyPath";
        }

        if (filePath.Length > 259)
        {
            return "PathTooLong (>259 chars)";
        }

        string? directory;

        try
        {
            directory = Path.GetDirectoryName(filePath);
        }
        catch (Exception)
        {
            return "InvalidPathFormat";
        }

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            return "DirectoryMissing (drive unplugged / folder moved?)";
        }

        if (!File.Exists(filePath))
        {
            return "FileNotFound (moved or deleted)";
        }

        try
        {
            using var stream = File.OpenRead(filePath);

            var buffer = new byte[1];

            stream.Read(buffer, 0, 1);

            // File exists and is readable - if it ended up in the
            // failing set anyway, something else (not access) is wrong.
            return "ReadableButStillFailed (needs separate investigation)";
        }
        catch (UnauthorizedAccessException)
        {
            return "AccessDenied (permissions)";
        }
        catch (IOException)
        {
            return "FileLocked (in use by another program)";
        }
        catch (Exception ex)
        {
            return $"OtherError: {ex.GetType().Name}";
        }
    }
}
