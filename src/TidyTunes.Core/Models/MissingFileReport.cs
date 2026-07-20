namespace TidyTunes.Core.Models;

// Result of the missing-file analysis: every LibraryFiles row whose
// FilePath no longer exists on disk, classified by what most likely
// happened to it. Reused by the cleanup step, so entries carry enough
// information (matched disk path, claiming row) to act on later.
public class MissingFileReport
{
    public long TotalRows { get; set; }

    public long FoundOnDisk { get; set; }

    public List<MissingFileEntry> MissingEntries { get; } = new();

    public Dictionary<string, long> CategoryCounts { get; } = new();
}


public class MissingFileEntry
{
    public long Id { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    // Disk file this row appears to correspond to now (if any).
    public string? MatchedDiskPath { get; set; }

    // Id of the live LibraryFiles row that already tracks
    // MatchedDiskPath, when the match is a stale duplicate.
    public long? ClaimedByRowId { get; set; }
}


public static class MissingFileCategory
{
    // The file was renamed on disk and the renamed file is ALREADY
    // tracked by another LibraryFiles row - this row is a stale
    // leftover from before the rename. Safe to delete the row.
    public const string StaleDuplicate = "StaleDuplicate";

    // A matching disk file exists but no row tracks it - this row
    // can be repointed (relinked) to the new path.
    public const string RelinkCandidate = "RelinkCandidate";

    // The parent folder itself no longer exists.
    public const string FolderGone = "FolderGone";

    // The row's path is under a library root that isn't reachable
    // right now (drive unplugged, folder missing). Not treated as
    // missing - no action should be taken on these.
    public const string RootUnavailable = "RootUnavailable";

    // No plausible match found on disk.
    public const string Unmatched = "Unmatched";
}
