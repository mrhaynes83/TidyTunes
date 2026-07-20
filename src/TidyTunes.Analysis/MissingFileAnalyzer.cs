using System.Text;
using System.Text.RegularExpressions;
using TidyTunes.Core.Models;
using TidyTunes.Data.Repositories;

namespace TidyTunes.Analysis;

// Investigates rows whose FilePath no longer exists on disk. The
// dominant cause in this library is files renamed on disk (usually a
// stripped "Artist - " prefix or "(Official Video)"-style suffix) and
// then re-imported by a later scan as a NEW row - leaving the old row
// stale. This analyzer proves that per-row by matching each missing
// row against the actual contents of its own folder, by filename
// variant and by exact file size, and checking whether the matched
// disk file is already tracked by another row.
//
// Folders are enumerated once each (not one File.Exists per row), so
// a 200k+ row library is analyzed in a single pass over its folders.
public class MissingFileAnalyzer
{
    private readonly LibraryFileRepository _repository;


    public MissingFileAnalyzer(LibraryFileRepository repository)
    {
        _repository = repository;
    }


    public MissingFileReport Analyze()
    {
        var report = new MissingFileReport();

        var rows = _repository.GetAllForMissingFileAnalysis();

        report.TotalRows = rows.Count;


        // Group rows by parent folder so each folder on disk is
        // enumerated exactly once and matching stays folder-local.

        var rowsByDirectory = new Dictionary<string, List<LibraryFile>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            string? directory;

            try
            {
                directory = Path.GetDirectoryName(row.FilePath);
            }
            catch (Exception)
            {
                directory = null;
            }

            if (string.IsNullOrEmpty(directory))
            {
                AddEntry(report, row, MissingFileCategory.Unmatched);
                continue;
            }

            if (!rowsByDirectory.TryGetValue(directory, out var directoryRows))
            {
                directoryRows = new List<LibraryFile>();
                rowsByDirectory[directory] = directoryRows;
            }

            directoryRows.Add(row);
        }


        var processed = 0;

        foreach (var (directory, directoryRows) in rowsByDirectory)
        {
            AnalyzeDirectory(report, directory, directoryRows);

            processed++;

            if (processed % 200 == 0)
            {
                Console.Write(
                    $"\rAnalyzed {processed:N0} of {rowsByDirectory.Count:N0} folders...");
            }
        }

        Console.Write(
            $"\rAnalyzed {rowsByDirectory.Count:N0} of {rowsByDirectory.Count:N0} folders...");

        Console.WriteLine();


        return report;
    }


    private static void AnalyzeDirectory(
        MissingFileReport report,
        string directory,
        List<LibraryFile> directoryRows)
    {
        if (!Directory.Exists(directory))
        {
            // Distinguish "this folder was removed" from "the whole
            // root/drive is unreachable" - the latter must never be
            // treated as missing files.
            var root = Path.GetPathRoot(directory);

            var category =
                string.IsNullOrEmpty(root) || !Directory.Exists(root)
                    ? MissingFileCategory.RootUnavailable
                    : MissingFileCategory.FolderGone;

            foreach (var row in directoryRows)
            {
                AddEntry(report, row, category);
            }

            return;
        }


        // One enumeration of the folder feeds three lookups:
        //   - exact name (existence / claim tracking)
        //   - normalized name variants (rename matching)
        //   - exact file size (rename matching when the name changed
        //     beyond recognition)

        var sizeByName = new Dictionary<string, long>(
            StringComparer.OrdinalIgnoreCase);

        var namesByVariant = new Dictionary<string, List<string>>(
            StringComparer.Ordinal);

        var namesBySize = new Dictionary<long, List<string>>();

        foreach (var path in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(path);

            long size;

            try
            {
                size = new FileInfo(path).Length;
            }
            catch (Exception)
            {
                size = -1;
            }

            sizeByName[name] = size;

            foreach (var variant in GetNameVariants(name))
            {
                if (!namesByVariant.TryGetValue(variant, out var variantNames))
                {
                    variantNames = new List<string>();
                    namesByVariant[variant] = variantNames;
                }

                variantNames.Add(name);
            }

            if (size > 0)
            {
                if (!namesBySize.TryGetValue(size, out var sizeNames))
                {
                    sizeNames = new List<string>();
                    namesBySize[size] = sizeNames;
                }

                sizeNames.Add(name);
            }
        }


        // Pass 1: find which rows still exist, and record which disk
        // names those rows claim - a missing row matching a claimed
        // disk file is a stale duplicate, not a relink candidate.

        var claimedBy = new Dictionary<string, long>(
            StringComparer.OrdinalIgnoreCase);

        var missingRows = new List<LibraryFile>();

        foreach (var row in directoryRows)
        {
            var name = Path.GetFileName(row.FilePath);

            if (sizeByName.ContainsKey(name))
            {
                report.FoundOnDisk++;
                claimedBy[name] = row.Id;
            }
            else
            {
                missingRows.Add(row);
            }
        }


        // Pass 2: classify each missing row against the folder index.

        foreach (var row in missingRows)
        {
            var match = FindMatch(row, namesByVariant, namesBySize, claimedBy);

            if (match == null)
            {
                AddEntry(report, row, MissingFileCategory.Unmatched);
                continue;
            }

            var (matchedName, claimedByRowId) = match.Value;

            var entry = AddEntry(
                report,
                row,
                claimedByRowId != null
                    ? MissingFileCategory.StaleDuplicate
                    : MissingFileCategory.RelinkCandidate);

            entry.MatchedDiskPath = Path.Combine(directory, matchedName);
            entry.ClaimedByRowId = claimedByRowId;
        }
    }


    // Returns the disk file this row most likely became, or null.
    // Preference order: filename-variant match, then unique file-size
    // match. Among variant matches a claimed file wins (strongest
    // evidence of the rename-then-rescan story); an unclaimed variant
    // match is only trusted when it is unambiguous.
    private static (string MatchedName, long? ClaimedByRowId)? FindMatch(
        LibraryFile row,
        Dictionary<string, List<string>> namesByVariant,
        Dictionary<long, List<string>> namesBySize,
        Dictionary<string, long> claimedBy)
    {
        var rowName = Path.GetFileName(row.FilePath);
        var rowExtension = Path.GetExtension(rowName);

        var candidates = new List<string>();

        foreach (var variant in GetNameVariants(rowName))
        {
            if (!namesByVariant.TryGetValue(variant, out var variantNames))
            {
                continue;
            }

            foreach (var name in variantNames)
            {
                if (!string.Equals(
                        Path.GetExtension(name),
                        rowExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!candidates.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    candidates.Add(name);
                }
            }
        }

        foreach (var name in candidates)
        {
            if (claimedBy.TryGetValue(name, out var claimingRowId))
            {
                return (name, claimingRowId);
            }
        }

        if (candidates.Count == 1)
        {
            return (candidates[0], null);
        }


        // Fall back to file size, but only on an exact, unique hit.

        if (row.FileSize > 0
            && namesBySize.TryGetValue(row.FileSize, out var sizeNames)
            && sizeNames.Count == 1)
        {
            var name = sizeNames[0];

            if (claimedBy.TryGetValue(name, out var claimingRowId))
            {
                return (name, claimingRowId);
            }

            return (name, null);
        }


        return null;
    }


    // Normalized keys a filename can be found under: the full base
    // name, the base with trailing "(...)"/"[...]" groups removed,
    // and each "Artist - Title" style prefix stripped (with and
    // without the suffix groups). Both the disk files and the DB rows
    // are expanded this way, so a rename in either direction (prefix
    // added or removed) still intersects.
    private static IEnumerable<string> GetNameVariants(string fileName)
    {
        var baseName = Normalize(Path.GetFileNameWithoutExtension(fileName));

        if (baseName.Length == 0)
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var withSuffixHandling in SuffixVariants(baseName))
        {
            if (seen.Add(withSuffixHandling))
            {
                yield return withSuffixHandling;
            }

            // Strip "Artist - " style prefixes at each " - " separator.
            var remainder = withSuffixHandling;

            var separatorIndex = remainder.IndexOf(" - ", StringComparison.Ordinal);

            while (separatorIndex >= 0)
            {
                remainder = remainder[(separatorIndex + 3)..].Trim();

                if (remainder.Length >= 3 && seen.Add(remainder))
                {
                    yield return remainder;
                }

                separatorIndex = remainder.IndexOf(" - ", StringComparison.Ordinal);
            }
        }
    }


    private static IEnumerable<string> SuffixVariants(string baseName)
    {
        yield return baseName;

        // Repeatedly drop trailing "(...)" / "[...]" groups, e.g.
        // "Song (Official Video) [HD]" -> "Song (Official Video)" -> "Song".
        var current = baseName;

        while (true)
        {
            var stripped = Regex.Replace(
                current,
                @"\s*[\(\[][^\(\)\[\]]*[\)\]]\s*$",
                "");

            stripped = stripped.Trim();

            if (stripped.Length < 3 || stripped == current)
            {
                yield break;
            }

            current = stripped;

            yield return current;
        }
    }


    private static string Normalize(string value)
    {
        return Regex
            .Replace(value.ToLowerInvariant(), @"\s+", " ")
            .Trim();
    }


    private static MissingFileEntry AddEntry(
        MissingFileReport report,
        LibraryFile row,
        string category)
    {
        var entry = new MissingFileEntry
        {
            Id = row.Id,
            FilePath = row.FilePath,
            Category = category
        };

        report.MissingEntries.Add(entry);

        report.CategoryCounts[category] =
            report.CategoryCounts.GetValueOrDefault(category) + 1;

        return entry;
    }


    // Writes the full classified list to a CSV in the Reports folder
    // and returns the path, so results can be reviewed outside the
    // console before any cleanup is run.
    public static string WriteCsvReport(
        MissingFileReport report,
        string reportsDirectory)
    {
        Directory.CreateDirectory(reportsDirectory);

        var reportPath = Path.Combine(
            reportsDirectory,
            $"MissingFiles_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var builder = new StringBuilder();

        builder.AppendLine("Id,Category,DbPath,MatchedDiskPath,ClaimedByRowId");

        foreach (var entry in report.MissingEntries)
        {
            builder.Append(entry.Id);
            builder.Append(',');
            builder.Append(entry.Category);
            builder.Append(',');
            builder.Append(CsvField(entry.FilePath));
            builder.Append(',');
            builder.Append(CsvField(entry.MatchedDiskPath ?? ""));
            builder.Append(',');
            builder.Append(entry.ClaimedByRowId?.ToString() ?? "");
            builder.AppendLine();
        }

        File.WriteAllText(reportPath, builder.ToString());

        return reportPath;
    }


    private static string CsvField(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
