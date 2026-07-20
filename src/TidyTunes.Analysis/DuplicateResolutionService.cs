using System.Text;
using TidyTunes.Core.Models;
using TidyTunes.Data;
using TidyTunes.Data.Repositories;

namespace TidyTunes.Analysis;

// Resolves exact-duplicate groups by keeping the best copy of each
// and moving the rest into a quarantine folder on the same volume
// (instant renames, trivially reversible). Nothing is ever deleted.
//
// "Best" means: passed the audio integrity check, then higher
// bitrate, then the shorter/cleaner path, then the older row.
// Quarantined rows stay in the database with OriginalPath set, so a
// restore is a move back plus clearing two fields.
public class DuplicateResolutionService
{
    private readonly DatabaseService _database;
    private readonly LibraryFileRepository _repository;


    public DuplicateResolutionService(
        DatabaseService database,
        LibraryFileRepository repository)
    {
        _database = database;
        _repository = repository;
    }


    public DuplicateResolutionResult Run(
        string musicRoot,
        string quarantineRoot)
    {
        var result = new DuplicateResolutionResult();

        Console.WriteLine("Rebuilding exact duplicate groups (SHA256)...");

        var analyzer = new DuplicateAnalyzer(_database);

        var groups = analyzer.FindExactDuplicates();

        Console.WriteLine($"Duplicate groups: {groups.Count:N0}");
        Console.WriteLine();

        if (groups.Count == 0)
        {
            return result;
        }


        var updates = new List<(long Id, string QuarantinePath, string OriginalPath)>();

        var processedGroups = 0;

        foreach (var group in groups)
        {
            processedGroups++;

            // Only consider copies whose file is actually present and
            // not already sitting in quarantine from a previous run.

            var candidates = group.Files
                .Where(f => f.IntegrityStatus != "FileNotFound"
                            && !f.FilePath.StartsWith(quarantineRoot, StringComparison.OrdinalIgnoreCase)
                            && File.Exists(f.FilePath))
                .ToList();

            if (candidates.Count < 2)
            {
                continue;
            }

            var keeper = PickBest(candidates);

            result.GroupsResolved++;

            foreach (var loser in candidates)
            {
                if (loser.Id == keeper.Id)
                {
                    continue;
                }

                var quarantinePath = BuildQuarantinePath(
                    loser.FilePath, musicRoot, quarantineRoot);

                try
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(quarantinePath)!);

                    File.Move(loser.FilePath, quarantinePath);

                    updates.Add((loser.Id, quarantinePath, loser.FilePath));

                    result.FilesQuarantined++;
                    result.BytesReclaimed += loser.FileSize;
                }
                catch (Exception ex)
                {
                    result.MoveFailed++;

                    Console.WriteLine(
                        $"ERROR moving {loser.FilePath}: {ex.Message}");
                }
            }

            if (processedGroups % 500 == 0)
            {
                Console.WriteLine(
                    $"[{processedGroups:N0}/{groups.Count:N0}] groups processed, " +
                    $"{result.FilesQuarantined:N0} files quarantined...");
            }

            // Commit move records periodically so an interruption
            // loses at most one batch of bookkeeping, not the lot.

            if (updates.Count >= 500)
            {
                _repository.UpdateQuarantineBatch(updates);
                updates.Clear();
            }
        }

        if (updates.Count > 0)
        {
            _repository.UpdateQuarantineBatch(updates);
        }

        return result;
    }


    // Report-only view of the same-recording duplicate detection -
    // shares the grouping rules with RunAudioDuplicates but moves
    // nothing, so it's safe to browse any time.
    public AudioDuplicateReport AnalyzeAudioDuplicates()
    {
        var report = new AudioDuplicateReport();

        var byRecording = LoadRecordingGroups();

        var ranked = new List<(string Keeper, List<LibraryFile> Extras, long Bytes)>();

        foreach (var group in byRecording.Values)
        {
            var candidates = group.Where(f => File.Exists(f.FilePath)).ToList();

            if (candidates.Count < 2)
            {
                continue;
            }

            var keeper = PickBest(candidates);

            var extras = candidates.Where(f => f.Id != keeper.Id).ToList();

            report.Groups++;
            report.SurplusFiles += extras.Count;

            var bytes = extras.Sum(f => f.FileSize);

            report.SurplusBytes += bytes;

            ranked.Add(($"{keeper.Title} [{keeper.BitRate} kbps]", extras, bytes));
        }

        foreach (var (keeper, extras, bytes) in ranked
            .OrderByDescending(g => g.Bytes).Take(5))
        {
            report.SampleLines.Add(
                $"keep: {keeper}  |  {extras.Count} extra cop{(extras.Count == 1 ? "y" : "ies")}, {bytes / 1048576.0:N1} MB");
        }

        return report;
    }


    private Dictionary<string, List<LibraryFile>> LoadRecordingGroups()
    {
        var byRecording = new Dictionary<string, List<LibraryFile>>();

        using var connection = _database.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            MusicBrainzRecordingId, Id, FilePath, FileSize,
            IntegrityStatus, BitRate, SampleRate, DurationSeconds,
            COALESCE(Title, ''), COALESCE(FileName, '')
        FROM LibraryFiles
        WHERE MusicBrainzRecordingId IS NOT NULL
          AND MusicBrainzRecordingId != ''
          AND (QuarantinedDate IS NULL OR QuarantinedDate = '')
          AND MusicBrainzRecordingId IN
          (
              SELECT MusicBrainzRecordingId FROM LibraryFiles
              WHERE MusicBrainzRecordingId IS NOT NULL
                AND MusicBrainzRecordingId != ''
                AND (QuarantinedDate IS NULL OR QuarantinedDate = '')
              GROUP BY MusicBrainzRecordingId HAVING COUNT(*) > 1
          );
        """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var recordingId = reader.GetString(0);

            if (!byRecording.TryGetValue(recordingId, out var list))
            {
                list = new List<LibraryFile>();
                byRecording[recordingId] = list;
            }

            list.Add(new LibraryFile
            {
                Id = reader.GetInt64(1),
                FilePath = reader.GetString(2),
                FileSize = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                IntegrityStatus = reader.IsDBNull(4) ? null : reader.GetString(4),
                BitRate = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                SampleRate = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                DurationSeconds = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                Title = reader.GetString(8),
                FileName = reader.GetString(9)
            });
        }

        return byRecording;
    }


    // Quality ranking shared by both passes: a copy that decodes
    // cleanly always beats one that doesn't, then bitrate, sample
    // rate, and file size decide, with the shorter/cleaner path and
    // older row as final tiebreaks.
    private static LibraryFile PickBest(List<LibraryFile> candidates)
    {
        return candidates
            .OrderByDescending(f => f.IntegrityStatus == "Passed" ? 1 : 0)
            .ThenByDescending(f => f.BitRate ?? 0)
            .ThenByDescending(f => f.SampleRate ?? 0)
            .ThenByDescending(f => f.FileSize)
            .ThenBy(f => f.FilePath.Length)
            .ThenBy(f => f.Id)
            .First();
    }


    // Second pass: same RECORDING (via AcoustID/MusicBrainz), different
    // files - alternate encodes of a song the library already has in
    // better quality. The best copy stays; the rest move to their own
    // quarantine folder. A duration tolerance keeps different edits
    // (radio vs. extended) that share a recording id from being
    // treated as copies of each other.
    public DuplicateResolutionResult RunAudioDuplicates(
        string musicRoot,
        string quarantineRoot)
    {
        var result = new DuplicateResolutionResult();

        const int durationToleranceSeconds = 5;

        Console.WriteLine("Loading identified files by recording...");

        var byRecording = new Dictionary<string, List<LibraryFile>>();

        using (var connection = _database.GetConnection())
        {
            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText =
            """
            SELECT
                MusicBrainzRecordingId,
                Id,
                FilePath,
                FileSize,
                IntegrityStatus,
                BitRate,
                SampleRate,
                DurationSeconds,
                COALESCE(Title, ''),
                COALESCE(FileName, '')
            FROM LibraryFiles
            WHERE MusicBrainzRecordingId IS NOT NULL
              AND MusicBrainzRecordingId != ''
              AND (QuarantinedDate IS NULL OR QuarantinedDate = '')
              AND (IntegrityStatus IS NULL OR IntegrityStatus != 'FileNotFound')
              AND MusicBrainzRecordingId IN
              (
                  SELECT MusicBrainzRecordingId
                  FROM LibraryFiles
                  WHERE MusicBrainzRecordingId IS NOT NULL
                    AND MusicBrainzRecordingId != ''
                    AND (QuarantinedDate IS NULL OR QuarantinedDate = '')
                  GROUP BY MusicBrainzRecordingId
                  HAVING COUNT(*) > 1
              );
            """;

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var recordingId = reader.GetString(0);

                if (!byRecording.TryGetValue(recordingId, out var list))
                {
                    list = new List<LibraryFile>();
                    byRecording[recordingId] = list;
                }

                list.Add(new LibraryFile
                {
                    Id = reader.GetInt64(1),
                    FilePath = reader.GetString(2),
                    FileSize = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                    IntegrityStatus = reader.IsDBNull(4) ? null : reader.GetString(4),
                    BitRate = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    SampleRate = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    DurationSeconds = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    Title = reader.GetString(8),
                    FileName = reader.GetString(9)
                });
            }
        }

        Console.WriteLine(
            $"Recordings with multiple copies: {byRecording.Count:N0}");
        Console.WriteLine();


        var updates = new List<(long Id, string QuarantinePath, string OriginalPath)>();

        var processedGroups = 0;

        foreach (var group in byRecording.Values)
        {
            processedGroups++;

            var candidates = group
                .Where(f => !f.FilePath.StartsWith(quarantineRoot, StringComparison.OrdinalIgnoreCase)
                            && File.Exists(f.FilePath))
                .ToList();

            if (candidates.Count < 2)
            {
                continue;
            }

            var keeper = PickBest(candidates);

            var resolvedAny = false;

            foreach (var loser in candidates)
            {
                if (loser.Id == keeper.Id)
                {
                    continue;
                }

                // Different edits sharing a recording id are not
                // duplicates - only quarantine when durations agree.

                var durationDelta = Math.Abs(
                    (loser.DurationSeconds ?? 0) - (keeper.DurationSeconds ?? 0));

                if (durationDelta > durationToleranceSeconds)
                {
                    result.SkippedDurationMismatch++;
                    continue;
                }

                // Live recordings are distinct performances, never a
                // "lower quality copy" of a studio track. If exactly
                // one side looks live, leave both alone; two copies of
                // the SAME live recording still dedupe normally.

                if (LooksLive(loser) != LooksLive(keeper))
                {
                    result.SkippedLiveMismatch++;
                    continue;
                }

                var quarantinePath = BuildQuarantinePath(
                    loser.FilePath, musicRoot, quarantineRoot);

                try
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(quarantinePath)!);

                    File.Move(loser.FilePath, quarantinePath);

                    updates.Add((loser.Id, quarantinePath, loser.FilePath));

                    result.FilesQuarantined++;
                    result.BytesReclaimed += loser.FileSize;

                    resolvedAny = true;
                }
                catch (Exception ex)
                {
                    result.MoveFailed++;

                    Console.WriteLine(
                        $"ERROR moving {loser.FilePath}: {ex.Message}");
                }
            }

            if (resolvedAny)
            {
                result.GroupsResolved++;
            }

            if (processedGroups % 2000 == 0)
            {
                Console.WriteLine(
                    $"[{processedGroups:N0}/{byRecording.Count:N0}] recordings processed, " +
                    $"{result.FilesQuarantined:N0} lower-quality copies quarantined...");
            }

            if (updates.Count >= 500)
            {
                _repository.UpdateQuarantineBatch(updates);
                updates.Clear();
            }
        }

        if (updates.Count > 0)
        {
            _repository.UpdateQuarantineBatch(updates);
        }

        return result;
    }


    // "live" as a standalone word (word boundaries keep "Alive" and
    // "Deliverance" from matching), plus common live-album markers.
    private static readonly System.Text.RegularExpressions.Regex LiveMarker =
        new(@"\blive\b|\bunplugged\b|\bin concert\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
            | System.Text.RegularExpressions.RegexOptions.Compiled);


    private static bool LooksLive(LibraryFile file)
    {
        return LiveMarker.IsMatch(file.Title ?? "")
            || LiveMarker.IsMatch(file.FileName ?? "");
    }


    // Mirrors the file's position under the music root inside the
    // quarantine folder, so restores are unambiguous and same-named
    // files from different folders can't collide.
    private static string BuildQuarantinePath(
        string filePath,
        string musicRoot,
        string quarantineRoot)
    {
        string relative;

        if (filePath.StartsWith(musicRoot, StringComparison.OrdinalIgnoreCase))
        {
            relative = filePath[musicRoot.Length..]
                .TrimStart(Path.DirectorySeparatorChar);
        }
        else
        {
            relative = Path.GetFileName(filePath);
        }

        var target = Path.Combine(quarantineRoot, relative);

        if (!File.Exists(target))
        {
            return target;
        }

        var directory = Path.GetDirectoryName(target)!;
        var baseName = Path.GetFileNameWithoutExtension(target);
        var extension = Path.GetExtension(target);

        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(
                directory, $"{baseName} ({i}){extension}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }


    public string WriteLog(
        string logsDirectory,
        DuplicateResolutionResult result,
        DateTime startTime,
        string passName = "Exact")
    {
        Directory.CreateDirectory(logsDirectory);

        var logPath = Path.Combine(
            logsDirectory,
            $"DuplicateResolution_{passName}_{startTime:yyyyMMdd_HHmmss}.log");

        var builder = new StringBuilder();

        builder.AppendLine($"Duplicate Resolution ({passName})");
        builder.AppendLine($"Started:           {startTime:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Finished:          {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine($"Groups resolved:   {result.GroupsResolved:N0}");
        builder.AppendLine($"Files quarantined: {result.FilesQuarantined:N0}");
        builder.AppendLine($"Moves failed:      {result.MoveFailed:N0}");
        builder.AppendLine($"Space reclaimed:   {result.BytesReclaimed:N0} bytes");
        builder.AppendLine($"Skipped (duration mismatch): {result.SkippedDurationMismatch:N0}");
        builder.AppendLine($"Skipped (live protection):   {result.SkippedLiveMismatch:N0}");

        File.WriteAllText(logPath, builder.ToString());

        return logPath;
    }
}


public class AudioDuplicateReport
{
    public long Groups { get; set; }

    public long SurplusFiles { get; set; }

    public long SurplusBytes { get; set; }

    public List<string> SampleLines { get; } = new();
}


public class DuplicateResolutionResult
{
    public long GroupsResolved { get; set; }

    public long FilesQuarantined { get; set; }

    public long MoveFailed { get; set; }

    public long BytesReclaimed { get; set; }

    public long SkippedDurationMismatch { get; set; }

    public long SkippedLiveMismatch { get; set; }
}
