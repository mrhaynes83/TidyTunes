using System.IO.Compression;
using System.Text;
using TidyTunes.Core.Models;
using TidyTunes.Data.Repositories;

namespace TidyTunes.Analysis;

// Applies the results of MissingFileAnalyzer to the database:
//   - StaleDuplicate rows are deleted (their file was renamed on disk
//     and the renamed file already has its own row)
//   - RelinkCandidate rows are repointed at their matched disk file
//
// Audio files on disk are never touched - this only corrects the
// database. The database file is copied to the Backups folder before
// any change, and a summary log is written afterwards, per the
// project's preview/confirm/log/rollback safety rules.
public class StaleEntryCleanupService
{
    private readonly LibraryFileRepository _repository;
    private readonly string _databasePath;


    public StaleEntryCleanupService(
        LibraryFileRepository repository,
        string databasePath)
    {
        _repository = repository;
        _databasePath = databasePath;
    }


    // Backups are standard .zip archives (readable on Windows, macOS
    // and Linux without extra tools) and the folder is pruned to the
    // retention limit - oldest first. retention <= 0 keeps everything.
    public string BackupDatabase(string backupsDirectory, int retention = 3)
    {
        Directory.CreateDirectory(backupsDirectory);

        var backupPath = Path.Combine(
            backupsDirectory,
            $"TidyTunes_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        using (var zip = System.IO.Compression.ZipFile.Open(
            backupPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(
                _databasePath,
                Path.GetFileName(_databasePath),
                System.IO.Compression.CompressionLevel.Optimal);
        }

        if (retention > 0)
        {
            var backups = Directory
                .EnumerateFiles(backupsDirectory)
                .Where(f => Path.GetFileName(f).StartsWith("TidyTunes_")
                            && (f.EndsWith(".zip") || f.EndsWith(".db")))
                .OrderByDescending(File.GetLastWriteTime)
                .ToList();

            foreach (var old in backups.Skip(retention))
            {
                try
                {
                    File.Delete(old);
                }
                catch (Exception)
                {
                    // A locked old backup just stays until next time.
                }
            }
        }

        return backupPath;
    }


    public StaleCleanupResult Apply(MissingFileReport report)
    {
        var result = new StaleCleanupResult();

        var staleIds = new List<long>();

        var relinks = new List<(long Id, string NewPath, string NewFileName)>();

        // A disk file can only be relinked to one row; if several
        // missing rows matched the same file, the first wins and the
        // rest are skipped (FilePath is UNIQUE in the database).
        var relinkTargets = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in report.MissingEntries)
        {
            switch (entry.Category)
            {
                case MissingFileCategory.StaleDuplicate:
                    staleIds.Add(entry.Id);
                    break;

                case MissingFileCategory.RelinkCandidate:
                    if (entry.MatchedDiskPath == null
                        || !relinkTargets.Add(entry.MatchedDiskPath))
                    {
                        result.RelinksSkipped++;
                        break;
                    }

                    relinks.Add((
                        entry.Id,
                        entry.MatchedDiskPath,
                        Path.GetFileName(entry.MatchedDiskPath)));
                    break;
            }
        }

        result.RowsDeleted = _repository.DeleteBatch(staleIds);

        result.RowsRelinked = _repository.UpdateFilePathsBatch(relinks);

        return result;
    }


    public string WriteLog(
        string logsDirectory,
        MissingFileReport report,
        StaleCleanupResult result,
        string backupPath,
        DateTime startTime)
    {
        Directory.CreateDirectory(logsDirectory);

        var logPath = Path.Combine(
            logsDirectory,
            $"StaleEntryCleanup_{startTime:yyyyMMdd_HHmmss}.log");

        var builder = new StringBuilder();

        builder.AppendLine("Stale Entry Cleanup");
        builder.AppendLine($"Started:      {startTime:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Finished:     {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Database:     {_databasePath}");
        builder.AppendLine($"Backup:       {backupPath}");
        builder.AppendLine();

        builder.AppendLine($"Total rows analyzed:   {report.TotalRows:N0}");
        builder.AppendLine($"Found on disk:         {report.FoundOnDisk:N0}");
        builder.AppendLine($"Missing rows:          {report.MissingEntries.Count:N0}");
        builder.AppendLine();

        foreach (var category in report.CategoryCounts.OrderByDescending(c => c.Value))
        {
            builder.AppendLine($"  {category.Key}: {category.Value:N0}");
        }

        builder.AppendLine();

        builder.AppendLine($"Rows deleted:          {result.RowsDeleted:N0}");
        builder.AppendLine($"Rows relinked:         {result.RowsRelinked:N0}");
        builder.AppendLine($"Relinks skipped:       {result.RelinksSkipped:N0}");

        File.WriteAllText(logPath, builder.ToString());

        return logPath;
    }
}


public class StaleCleanupResult
{
    public long RowsDeleted { get; set; }

    public long RowsRelinked { get; set; }

    public long RelinksSkipped { get; set; }
}
