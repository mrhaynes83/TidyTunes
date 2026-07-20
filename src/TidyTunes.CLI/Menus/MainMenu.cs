using TidyTunes.CLI.UI;
using TidyTunes.Analysis;
using TidyTunes.Data;
using TidyTunes.Data.Migrations;
using TidyTunes.Scanner.Migration;
using TidyTunes.Scanner.Services;
using TidyTunes.Core.Models;
using TidyTunes.Data.Repositories;

namespace TidyTunes.CLI.Menus;

public class MainMenu
{
    private readonly AppSettings _settings;
    private readonly DatabaseService _database;
    private readonly string _settingsPath;

    public MainMenu(
        AppSettings settings,
        DatabaseService database,
        string settingsPath)
    {
        _settings = settings;
        _database = database;
        _settingsPath = settingsPath;

        var migrator = new DatabaseMigrator(_database);
        migrator.ApplyMigrations();
    }


    private List<(string Section, MenuItem[] Items)> BuildMenu()
    {
        return new List<(string, MenuItem[])>
        {
            ("Library", new[]
            {
                new MenuItem { Key = "1", Title = "Scan Music Folder",
                    Description = "Add/refresh files from a folder into the database",
                    Details = new[] { "Recursively scans a folder you choose",
                        "Reads tags and file info into the database",
                        "Never modifies any audio file",
                        "DRM-protected files (.m4p) cannot be read and are counted separately" },
                    Handler = ScanMusicFolder },
                new MenuItem { Key = "2", Title = "Refresh Metadata (tags)",
                    Description = "Re-read embedded tags; fills blanks only",
                    Details = new[] { "Re-reads every file's embedded tags in parallel",
                        "Fills database blanks (artist, genre, BPM, key...)",
                        "Existing database values are never overwritten" },
                    Handler = RefreshMetadata },
                new MenuItem { Key = "3", Title = "Identify Tracks (AcoustID)",
                    Description = "Fingerprint + match against MusicBrainz",
                    Details = new[] { "Fingerprints unidentified files (CPU heavy)",
                        "Looks each up at AcoustID (3 requests/sec - can take hours)",
                        "Fills blank metadata from confident matches; resumable" },
                    Handler = IdentifyTracks },
                new MenuItem { Key = "4", Title = "Enrich Year/Genre (MusicBrainz)",
                    Description = "Fill missing year/genre for identified tracks",
                    Details = new[] { "Looks up identified recordings at MusicBrainz (1/sec)",
                        "Fills missing Year and Genre only; resumable" },
                    Handler = EnrichYearGenre },
                new MenuItem { Key = "5", Title = "Library Statistics",
                    Description = "Counts, coverage, health at a glance",
                    Handler = ShowStatistics },
                new MenuItem { Key = "6", Title = "Search & Filter",
                    Description = "Find tracks by any field; saved filters",
                    Handler = SearchAndFilter },
            }),
            ("Analysis", new[]
            {
                new MenuItem { Key = "7", Title = "Exact Duplicates Report",
                    Description = "Byte-identical copies (SHA-256), report only",
                    Handler = FindDuplicates },
                new MenuItem { Key = "8", Title = "Audio Duplicates Report",
                    Description = "Same recording, different files - report only",
                    Handler = AudioDuplicatesReport },
                new MenuItem { Key = "9", Title = "Missing Metadata Report",
                    Description = "Files lacking artist/album/title",
                    Handler = FindMissingMetadata },
                new MenuItem { Key = "10", Title = "Quality Issues Report",
                    Description = "Low bitrate, mono, suspect FLACs, zero-byte",
                    Handler = QualityIssuesReport },
                new MenuItem { Key = "11", Title = "Verify Audio Integrity",
                    Description = "Full decode test of every file (slow)",
                    Details = new[] { "Decodes every unchecked file end-to-end with ffmpeg",
                        "Flags corrupt/unplayable files as issues",
                        "Multi-hour on a large library; resumable" },
                    Handler = VerifyAudioIntegrity },
                new MenuItem { Key = "12", Title = "Analyze Missing Files",
                    Description = "Find database rows whose file moved/vanished",
                    Handler = AnalyzeMissingFiles },
                new MenuItem { Key = "13", Title = "Diagnose File Access",
                    Description = "Why unreadable: locked, permissions, path",
                    Handler = DiagnoseFileAccess },
            }),
            ("Cleanup", new[]
            {
                new MenuItem { Key = "14", Title = "Review Issues",
                    Description = "All recorded issues grouped by type",
                    Handler = ReviewIssues },
                new MenuItem { Key = "15", Title = "Fix Issues",
                    Description = "Guided fixes: fill metadata, purge, clean tags",
                    Handler = FixSelectedIssues },
                new MenuItem { Key = "16", Title = "Resolve Duplicates",
                    Description = "Keep best copy; move extras to quarantine",
                    Details = new[] { "Re-detects exact and same-recording duplicates",
                        "Keeps the best copy (plays cleanly > bitrate > sample rate)",
                        "MOVES duplicate copies to the quarantine folder",
                        "Database is backed up first; every move is recorded and reversible" },
                    Dangers = new[] { "Thousands of files may be moved out of your library folders",
                        "Live recordings and different edits are protected, but no matcher is perfect",
                        "If you later DELETE the quarantine folder, those files are gone for good" },
                    Handler = ResolveDuplicates },
                new MenuItem { Key = "17", Title = "Clean Up Stale Entries",
                    Description = "Delete DB rows for renamed/vanished files",
                    Details = new[] { "Finds database rows whose file no longer exists",
                        "Relinks rows to renamed files where a confident match exists",
                        "Deletes rows whose file is tracked under another entry",
                        "Database is backed up first" },
                    Dangers = new[] { "Deleted database rows cannot be recovered except from the backup",
                        "Audio files themselves are NOT touched by this action" },
                    Handler = CleanUpStaleEntries },
            }),
            ("Organization", new[]
            {
                new MenuItem { Key = "18", Title = "Preview Organization Plan",
                    Description = "Show planned moves; changes nothing",
                    Handler = PreviewOrganizationPlan },
                new MenuItem { Key = "19", Title = "Apply Organization",
                    Description = "Execute the plan: move/rename files",
                    Details = new[] { "Moves files according to the previewed plan and your template",
                        "You can choose the destination root before it runs",
                        "Files without artist/title stay where they are",
                        "Database is backed up first; a full move log is written" },
                    Dangers = new[] { "This MOVES real files on disk - potentially tens of thousands",
                        "Interrupting mid-run can leave files in both old and new locations",
                        "Only proceed when the preview (18) looked right" },
                    Handler = ApplyOrganizationChanges },
            }),
            ("Reports", new[]
            {
                new MenuItem { Key = "20", Title = "Generate Library Report",
                    Description = "Full health/coverage report to Reports folder",
                    Handler = GenerateLibraryReport },
                new MenuItem { Key = "21", Title = "Export Results",
                    Description = "CSV exports: corrupt list, dupes, search hits",
                    Handler = ExportResults },
            }),
            ("System", new[]
            {
                new MenuItem { Key = "22", Title = "Database Maintenance",
                    Description = "Compute missing hashes; housekeeping",
                    Handler = RunDatabaseMaintenance },
                new MenuItem { Key = "23", Title = "Settings",
                    Description = "View and edit all settings incl. template",
                    Handler = EditSettings },
                new MenuItem { Key = "24", Title = "Database Status",
                    Description = "Schema version, row counts, file info",
                    Handler = ShowDatabaseStatus },
                new MenuItem { Key = "25", Title = "Import Legacy Inventory",
                    Description = "One-time import from the old inventory table",
                    Details = new[] { "Migrates rows from the legacy Files table",
                        "Only needed once on an old database - normally skip this" },
                    Handler = ImportInventory },
            }),
        };
    }


    public void Run()
    {
        var menu = BuildMenu();

        var lookup = menu
            .SelectMany(s => s.Items)
            .ToDictionary(i => i.Key, i => i);

        while (true)
        {
            // Console.Clear() throws when output is redirected
            // (piped runs, scripted use), so only clear a real console.
            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }

            ConsoleBanner.Show();

            ShowDashboard();

            Console.WriteLine();

            MenuScreen.Render(menu);

            Console.WriteLine(" Q.  Quit");
            Console.WriteLine();

            Console.Write("Select option: ");

            var choice = Console.ReadLine()?.Trim();

            Console.WriteLine();

            if (string.Equals(choice, "q", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (choice != null && lookup.TryGetValue(choice, out var item))
            {
                if (MenuScreen.Confirm(item))
                {
                    item.Handler();
                }
                else
                {
                    Pause();
                }
            }
            else
            {
                Console.WriteLine("Unknown option.");
                Pause();
            }
        }
    }


    private void ImportInventory()
    {
        Console.WriteLine(
            "Starting inventory migration...");

        var migrator =
            new ExistingInventoryMigrator(
                _settings.DatabasePath);

        var count = migrator.Migrate();

        Console.WriteLine();

        Console.WriteLine(
            $"Migration complete: {count:N0} files processed.");

        Pause();
    }


    private void ScanMusicFolder()
    {
        Console.WriteLine("Enter music folder path:");
        Console.Write("> ");

        var folder = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(folder))
        {
            Console.WriteLine("No folder entered.");
            Pause();
            return;
        }

        if (!Directory.Exists(folder))
        {
            Console.WriteLine("Folder does not exist.");
            Pause();
            return;
        }


        var scanner =
            new MusicScanner(_database);

        var count =
            scanner.Scan(folder);


        Console.WriteLine();

        Console.WriteLine(
            $"Scan complete: {count:N0} files processed.");

        Pause();
    }


    private void RefreshMetadata()
    {
        var repository =
            new LibraryFileRepository(_database);

        var extractor =
            new MetadataExtractor();

        var refresher =
            new TagMetadataRefreshService(
                repository,
                extractor);

        refresher.Run();

        Pause();
    }


    private void ShowStatistics()
    {
        var repository =
            new LibraryFileRepository(_database);

        Console.WriteLine("Loading library statistics...");

        var stats = repository.GetStatistics();

        if (!Console.IsOutputRedirected)
        {
            Console.Clear();
        }

        var divider = new string('=', 41);

        Console.WriteLine(divider);
        Console.WriteLine("Library Statistics");
        Console.WriteLine(divider);
        Console.WriteLine();

        Console.WriteLine("Total Files:");
        Console.WriteLine($"{stats.TotalFiles:N0}");
        Console.WriteLine();

        Console.WriteLine("Artists:");
        Console.WriteLine($"{stats.ArtistCount:N0}");
        Console.WriteLine();

        Console.WriteLine("Albums:");
        Console.WriteLine($"{stats.AlbumCount:N0}");
        Console.WriteLine();
        Console.WriteLine();

        Console.WriteLine("File Types");
        Console.WriteLine();

        foreach (var fileType in stats.FileTypeCounts)
        {
            Console.WriteLine($"{fileType.Key}:");
            Console.WriteLine($"{fileType.Value:N0}");
            Console.WriteLine();
        }
        Console.WriteLine();

        Console.WriteLine("Metadata Health");
        Console.WriteLine();

        Console.WriteLine("Missing Artist:");
        Console.WriteLine($"{stats.MissingArtist:N0}");
        Console.WriteLine();

        Console.WriteLine("Missing Album:");
        Console.WriteLine($"{stats.MissingAlbum:N0}");
        Console.WriteLine();

        Console.WriteLine("Missing Title:");
        Console.WriteLine($"{stats.MissingTitle:N0}");
        Console.WriteLine();
        Console.WriteLine();

        Console.WriteLine("Hash Status");
        Console.WriteLine();

        Console.WriteLine("MD5 Complete:");
        Console.WriteLine($"{stats.Md5Complete:N0}");
        Console.WriteLine();

        Console.WriteLine("SHA256 Complete:");
        Console.WriteLine($"{stats.Sha256Complete:N0}");
        Console.WriteLine();
        Console.WriteLine();

        Console.WriteLine("Database Health");
        Console.WriteLine();

        Console.WriteLine("Duplicate Groups:");
        Console.WriteLine($"{stats.DuplicateGroups:N0}");
        Console.WriteLine();

        Console.WriteLine("Issues:");
        Console.WriteLine($"{stats.Issues:N0}");
        Console.WriteLine();

        Console.WriteLine(divider);

        Pause();
    }


    private void DiagnoseFileAccess()
    {
        var repository =
            new LibraryFileRepository(_database);

        Console.WriteLine(
            "Checking files still missing hashes for access issues...");

        var files = repository.GetMissingHashes();

        Console.WriteLine($"Checking {files.Count:N0} files...");
        Console.WriteLine();

        var diagnostic = new FileAccessDiagnostic();

        diagnostic.Diagnose(files);

        Console.WriteLine("=================================");
        Console.WriteLine("File Access Diagnostic");
        Console.WriteLine("=================================");
        Console.WriteLine();

        foreach (var category in diagnostic.CategoryCounts.OrderByDescending(c => c.Value))
        {
            Console.WriteLine($"{category.Key}: {category.Value:N0}");

            foreach (var sample in diagnostic.Samples[category.Key])
            {
                Console.WriteLine($"    {sample}");
            }

            Console.WriteLine();
        }

        Console.WriteLine("=================================");

        Pause();
    }


    private void AnalyzeMissingFiles()
    {
        var report = RunMissingFileAnalysis();

        Console.WriteLine();

        PrintMissingFileSummary(report);

        if (report.MissingEntries.Count > 0)
        {
            var reportPath =
                MissingFileAnalyzer.WriteCsvReport(
                    report,
                    Path.Combine(GetProjectRoot(), "Reports"));

            Console.WriteLine();

            Console.WriteLine($"Full results written to:");
            Console.WriteLine($"    {reportPath}");
        }

        Pause();
    }


    private void CleanUpStaleEntries()
    {
        var report = RunMissingFileAnalysis();

        Console.WriteLine();

        PrintMissingFileSummary(report);

        var staleCount = report.CategoryCounts.GetValueOrDefault(
            MissingFileCategory.StaleDuplicate);

        var relinkCount = report.CategoryCounts.GetValueOrDefault(
            MissingFileCategory.RelinkCandidate);

        if (staleCount == 0 && relinkCount == 0)
        {
            Console.WriteLine();

            Console.WriteLine(
                "Nothing to clean up: no stale duplicates or relink candidates found.");

            Pause();
            return;
        }

        Console.WriteLine();

        Console.WriteLine("Planned changes (database rows only - no audio files are touched):");
        Console.WriteLine($"    Delete {staleCount:N0} stale duplicate row(s)");
        Console.WriteLine($"    Relink {relinkCount:N0} row(s) to their renamed file");
        Console.WriteLine();

        Console.WriteLine(
            "The database will be backed up to Data\\Backups first.");

        if (_settings.RequireDeleteConfirmation)
        {
            Console.WriteLine();

            Console.Write("Type YES to proceed: ");

            var confirmation = Console.ReadLine();

            if (!string.Equals(confirmation?.Trim(), "YES", StringComparison.Ordinal))
            {
                Console.WriteLine();
                Console.WriteLine("Cleanup cancelled. No changes were made.");

                Pause();
                return;
            }
        }

        var startTime = DateTime.Now;

        var repository =
            new LibraryFileRepository(_database);

        var cleanupService =
            new StaleEntryCleanupService(
                repository,
                _settings.DatabasePath);

        Console.WriteLine();

        Console.WriteLine("Backing up database...");

        var backupPath = cleanupService.BackupDatabase(
            GetBackupsDirectory(), _settings.BackupRetention);

        Console.WriteLine($"    {backupPath}");
        Console.WriteLine();

        Console.WriteLine("Applying changes...");

        var result = cleanupService.Apply(report);

        var logPath = cleanupService.WriteLog(
            Path.Combine(GetProjectRoot(), "Logs"),
            report,
            result,
            backupPath,
            startTime);

        Console.WriteLine();

        Console.WriteLine($"Rows deleted:    {result.RowsDeleted:N0}");
        Console.WriteLine($"Rows relinked:   {result.RowsRelinked:N0}");
        Console.WriteLine($"Relinks skipped: {result.RelinksSkipped:N0}");
        Console.WriteLine();

        Console.WriteLine($"Log written to:");
        Console.WriteLine($"    {logPath}");

        Pause();
    }


    private MissingFileReport RunMissingFileAnalysis()
    {
        var repository =
            new LibraryFileRepository(_database);

        var analyzer =
            new MissingFileAnalyzer(repository);

        Console.WriteLine(
            "Analyzing missing files (comparing database against disk)...");

        Console.WriteLine();

        return analyzer.Analyze();
    }


    private void PrintMissingFileSummary(MissingFileReport report)
    {
        Console.WriteLine("=================================");
        Console.WriteLine("Missing File Analysis");
        Console.WriteLine("=================================");
        Console.WriteLine();

        Console.WriteLine($"Total rows:    {report.TotalRows:N0}");
        Console.WriteLine($"Found on disk: {report.FoundOnDisk:N0}");
        Console.WriteLine($"Missing:       {report.MissingEntries.Count:N0}");
        Console.WriteLine();

        foreach (var category in report.CategoryCounts.OrderByDescending(c => c.Value))
        {
            Console.WriteLine($"{category.Key}: {category.Value:N0}");

            var samples = report.MissingEntries
                .Where(e => e.Category == category.Key)
                .Take(3);

            foreach (var sample in samples)
            {
                Console.WriteLine($"    {sample.FilePath}");

                if (sample.MatchedDiskPath != null)
                {
                    Console.WriteLine($"        -> {sample.MatchedDiskPath}");
                }
            }

            Console.WriteLine();
        }

        Console.WriteLine("=================================");
    }


    // The project folder that Config/Data/Reports/Logs live under,
    // derived from the database path (…\Data\Database\TidyTunes.db).
    private string GetProjectRoot()
    {
        var databaseDirectory =
            Path.GetDirectoryName(_settings.DatabasePath)
            ?? throw new InvalidOperationException(
                "DatabasePath has no parent directory.");

        return Path.GetFullPath(
            Path.Combine(databaseDirectory, "..", ".."));
    }


    private void VerifyAudioIntegrity()
    {
        var repository =
            new LibraryFileRepository(_database);

        var integrityService =
            new AudioIntegrityService(
                repository,
                _settings.FfmpegPath);

        if (!integrityService.IsAvailable(out var resolvedPath))
        {
            Console.WriteLine(
                $"ffmpeg not found at '{_settings.FfmpegPath}'. " +
                "Download it from https://ffmpeg.org/download.html " +
                "and set FfmpegPath in settings.json.");

            Console.WriteLine($"(Looked for: {resolvedPath})");

            Pause();
            return;
        }

        integrityService.Run();

        Pause();
    }


    private void ResolveDuplicates()
    {
        var repository =
            new LibraryFileRepository(_database);

        var resolutionService =
            new DuplicateResolutionService(_database, repository);

        var quarantineRoot = Path.Combine(
            _settings.MusicLibrary, "_TidyTunes_Quarantine");

        var exactQuarantine = Path.Combine(quarantineRoot, "Exact");
        var lowerQualityQuarantine = Path.Combine(quarantineRoot, "LowerQuality");

        Console.WriteLine(
            "Duplicate copies will be MOVED (never deleted) to:");
        Console.WriteLine($"    {exactQuarantine}  (byte-identical copies)");
        Console.WriteLine($"    {lowerQualityQuarantine}  (same recording, lower quality)");
        Console.WriteLine(
            "The best copy of each group stays " +
            "(integrity > bitrate > sample rate > size > path).");
        Console.WriteLine(
            "The database will be backed up first.");

        if (_settings.RequireDeleteConfirmation)
        {
            Console.WriteLine();

            Console.Write("Type YES to proceed: ");

            var confirmation = Console.ReadLine();

            if (!string.Equals(confirmation?.Trim(), "YES", StringComparison.Ordinal))
            {
                Console.WriteLine();
                Console.WriteLine("Cancelled. No changes were made.");

                Pause();
                return;
            }
        }

        var startTime = DateTime.Now;

        var cleanupService =
            new StaleEntryCleanupService(repository, _settings.DatabasePath);

        Console.WriteLine();
        Console.WriteLine("Backing up database...");

        var backupPath = cleanupService.BackupDatabase(
            GetBackupsDirectory(), _settings.BackupRetention);

        Console.WriteLine($"    {backupPath}");
        Console.WriteLine();

        Console.WriteLine("--- Pass 1: exact duplicates (SHA256) ---");
        Console.WriteLine();

        var exactResult = resolutionService.Run(
            _settings.MusicLibrary, exactQuarantine);

        var exactLog = resolutionService.WriteLog(
            Path.Combine(GetProjectRoot(), "Logs"), exactResult, startTime, "Exact");

        Console.WriteLine();
        Console.WriteLine($"Groups resolved:   {exactResult.GroupsResolved:N0}");
        Console.WriteLine($"Files quarantined: {exactResult.FilesQuarantined:N0}");
        Console.WriteLine($"Moves failed:      {exactResult.MoveFailed:N0}");
        Console.WriteLine($"Space reclaimed:   {FormatBytes(exactResult.BytesReclaimed)}");
        Console.WriteLine();

        Console.WriteLine("--- Pass 2: same recording, lower quality ---");
        Console.WriteLine();

        var audioStartTime = DateTime.Now;

        var audioResult = resolutionService.RunAudioDuplicates(
            _settings.MusicLibrary, lowerQualityQuarantine);

        var audioLog = resolutionService.WriteLog(
            Path.Combine(GetProjectRoot(), "Logs"), audioResult, audioStartTime, "LowerQuality");

        Console.WriteLine();
        Console.WriteLine($"Recordings resolved:  {audioResult.GroupsResolved:N0}");
        Console.WriteLine($"Files quarantined:    {audioResult.FilesQuarantined:N0}");
        Console.WriteLine($"Duration mismatches:  {audioResult.SkippedDurationMismatch:N0} (left alone)");
        Console.WriteLine($"Live protection:      {audioResult.SkippedLiveMismatch:N0} (left alone)");
        Console.WriteLine($"Moves failed:         {audioResult.MoveFailed:N0}");
        Console.WriteLine($"Space reclaimed:      {FormatBytes(audioResult.BytesReclaimed)}");
        Console.WriteLine();
        Console.WriteLine($"Logs written to:");
        Console.WriteLine($"    {exactLog}");
        Console.WriteLine($"    {audioLog}");

        Pause();
    }


    private void PreviewOrganizationPlan()
    {
        var repository =
            new LibraryFileRepository(_database);

        var organizationService =
            new OrganizationService(_database, repository);

        var organizedRoot = GetOrganizedRoot();

        Console.WriteLine($"Destination root [{organizedRoot}]");
        Console.Write("ENTER keeps it, or type a different drive/folder: ");

        var overrideRoot = Console.ReadLine()?.Trim();

        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            organizedRoot = overrideRoot;
            _settings.OrganizedRoot = overrideRoot;
            SaveSettings();
        }

        Console.WriteLine();
        Console.WriteLine("Building organization plan...");
        Console.WriteLine($"Template: {_settings.OrganizationTemplate}   (change in Settings)");
        Console.WriteLine($"Effects go to: {GetEffectsRoot()}");
        Console.WriteLine();

        var (planned, alreadyOrganized, skipped) =
            organizationService.BuildPlan(
                organizedRoot,
                GetEffectsRoot(),
                _settings.OrganizationTemplate);

        Console.WriteLine($"Files to move:            {planned:N0}");
        Console.WriteLine($"Already organized:        {alreadyOrganized:N0}");
        Console.WriteLine($"Skipped (missing artist/title or unresolvable): {skipped:N0}");
        Console.WriteLine();

        var samples = organizationService.GetPlanSamples(8);

        if (samples.Count > 0)
        {
            Console.WriteLine("Sample moves:");

            foreach (var (oldPath, newPath) in samples)
            {
                Console.WriteLine($"    {oldPath}");
                Console.WriteLine($"        -> {newPath}");
            }

            Console.WriteLine();
            Console.WriteLine(
                "Run option 10 to apply this plan.");
        }

        Pause();
    }


    private void ApplyOrganizationChanges()
    {
        var repository =
            new LibraryFileRepository(_database);

        var organizationService =
            new OrganizationService(_database, repository);

        Console.WriteLine(
            "This will move files according to the current plan " +
            "(build/preview it with option 9 first).");

        Console.WriteLine(
            "The database will be backed up first.");

        if (_settings.RequireDeleteConfirmation)
        {
            Console.WriteLine();

            Console.Write("Type YES to proceed: ");

            var confirmation = Console.ReadLine();

            if (!string.Equals(confirmation?.Trim(), "YES", StringComparison.Ordinal))
            {
                Console.WriteLine();
                Console.WriteLine("Cancelled. No files were moved.");

                Pause();
                return;
            }
        }

        var startTime = DateTime.Now;

        var cleanupService =
            new StaleEntryCleanupService(repository, _settings.DatabasePath);

        Console.WriteLine();
        Console.WriteLine("Backing up database...");

        var backupPath = cleanupService.BackupDatabase(
            GetBackupsDirectory(), _settings.BackupRetention);

        Console.WriteLine($"    {backupPath}");
        Console.WriteLine();

        var result = organizationService.ApplyPlan();

        var logPath = organizationService.WriteLog(
            Path.Combine(GetProjectRoot(), "Logs"), result, startTime);

        Console.WriteLine();
        Console.WriteLine("Removing empty folders...");

        var removedFolders = organizationService.CleanEmptyDirectories(
            GetOrganizedRoot());

        Console.WriteLine();
        Console.WriteLine($"Files moved:     {result.Moved:N0}");
        Console.WriteLine($"Failed:          {result.Failed:N0}");
        Console.WriteLine($"Folders removed: {removedFolders:N0}");
        Console.WriteLine();
        Console.WriteLine($"Log written to:");
        Console.WriteLine($"    {logPath}");

        Pause();
    }


    private void ReviewIssues()
    {
        var repository =
            new LibraryFileRepository(_database);

        Console.WriteLine("Loading issue summary...");
        Console.WriteLine();

        var summary = repository.GetIssueSummary();

        Console.WriteLine("=================================");
        Console.WriteLine("Issue Review");
        Console.WriteLine("=================================");
        Console.WriteLine();

        if (summary.Count == 0)
        {
            Console.WriteLine("No issues recorded.");
        }

        foreach (var (issueType, status, count) in summary)
        {
            Console.WriteLine($"{issueType} [{status}]: {count:N0}");
        }

        Console.WriteLine();
        Console.WriteLine(
            "Use option 12 to apply fixes (fill metadata, purge dead rows, " +
            "export corrupt list).");

        Pause();
    }


    private void FixSelectedIssues()
    {
        Console.WriteLine("Fix Selected Issues");
        Console.WriteLine("-------------------");
        Console.WriteLine("1. Fill missing Artist/Title from filenames");
        Console.WriteLine("2. Purge rows whose files no longer exist");
        Console.WriteLine("3. Export corrupt-file re-download list");
        Console.WriteLine("4. Match artist from library dictionary (word-order tolerant)");
        Console.WriteLine("5. Clean dirty artist tags (URLs, junk, decoration)");
        Console.WriteLine("6. Merge artist variants + fix ALL-CAPS names");
        Console.WriteLine("7. Fix swapped artist/title fields");
        Console.WriteLine("Q. Back");
        Console.WriteLine();

        Console.Write("Select fix: ");

        var choice = Console.ReadLine();

        Console.WriteLine();

        switch (choice?.Trim())
        {
            case "1":
                FillMetadataFromFilenames();
                break;

            case "2":
                PurgeDeadRows();
                break;

            case "3":
                ExportCorruptFileList();
                break;

            case "4":
                MatchArtistsFromDictionary();
                break;

            case "5":
                CleanDirtyArtistTags();
                break;

            case "6":
                CanonicalizeArtists();
                break;

            case "7":
                FixSwappedArtistTitle();
                break;

            default:
                break;
        }
    }


    private void FixSwappedArtistTitle()
    {
        var repository =
            new LibraryFileRepository(_database);

        Console.WriteLine("Looking for swapped artist/title fields...");

        // Established artists (many files) by folded name; a TITLE
        // matching one of these while the ARTIST is unknown in the
        // library means the two fields are almost certainly reversed
        // ("Spicy McHaggis" by "Dropkick Murphys").

        var artistCounts = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var (artist, count) in repository.GetKnownArtists())
        {
            var key = TidyTunes.Core.ArtistNameFolding.FoldKey(artist);

            if (key.Length > 0)
            {
                artistCounts[key] =
                    artistCounts.GetValueOrDefault(key) + count;
            }
        }

        var rows = repository.GetAll();

        var swaps = new List<(long Id, string Artist, string Title)>();

        var samples = new List<string>();

        // A string with multi-credit separators is a CREDIT, never a
        // song title - it belongs on the artist side regardless of
        // which side is more famous ("Kiss" by "Art of Noise & Tom
        // Jones" stays exactly as credited).
        var creditMarkers = new System.Text.RegularExpressions.Regex(
            @"[,&]|\s(y|ft\.?|feat\.?|featuring|vs\.?)\s",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.Artist)
                || string.IsNullOrEmpty(row.Title))
            {
                continue;
            }

            var titleKey = TidyTunes.Core.ArtistNameFolding.FoldKey(row.Title);

            var artistKey = TidyTunes.Core.ArtistNameFolding.FoldKey(row.Artist);

            if (titleKey.Length == 0 || artistKey.Length == 0
                || titleKey == artistKey)
            {
                continue;
            }

            var titleAsArtist = artistCounts.GetValueOrDefault(titleKey);

            var artistAsArtist = artistCounts.GetValueOrDefault(artistKey);

            var titleIsCredit = creditMarkers.IsMatch(row.Title);

            var artistIsCredit = creditMarkers.IsMatch(row.Artist);

            // Swap ONLY the unambiguous case: the title is a WELL-
            // established artist, the current "artist" is unknown,
            // and NEITHER side is a multi-artist credit. Credits stay
            // where they are ("Kiss" by "Art of Noise & Tom Jones"),
            // and titles with commas ("Ob-La-Di, Ob-La-Da") are never
            // treated as credits.
            var shouldSwap =
                !titleIsCredit && !artistIsCredit
                && titleAsArtist >= 10 && artistAsArtist <= 2;

            if (shouldSwap)
            {
                swaps.Add((row.Id, row.Title ?? "", row.Artist ?? ""));

                if (samples.Count < 12)
                {
                    samples.Add(
                        $"    '{row.Artist}' / '{row.Title}'  ->  swapped");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Swapped rows found: {swaps.Count:N0}");
        Console.WriteLine();

        if (swaps.Count == 0)
        {
            Pause();
            return;
        }

        Console.WriteLine("Samples (artist / title as currently stored):");

        foreach (var sample in samples)
        {
            Console.WriteLine(sample);
        }

        Console.WriteLine();
        Console.WriteLine("Applying swaps...");

        repository.UpdateArtistTitleSwapBatch(swaps);

        Console.WriteLine();
        Console.WriteLine(
            $"Swapped {swaps.Count:N0} row(s). Run 9/10 to re-organize.");

        Pause();
    }


    private void CanonicalizeArtists()
    {
        var repository =
            new LibraryFileRepository(_database);

        Console.WriteLine("Grouping artist name variants...");

        var service = new ArtistCanonicalizationService();

        var renames = service.Plan(repository.GetKnownArtists());

        var variantCount = renames.Count(r => r.Reason == "variant");
        var capsCount = renames.Count(r => r.Reason == "caps");

        Console.WriteLine();
        Console.WriteLine($"Variant merges:  {variantCount:N0}");
        Console.WriteLine($"ALL-CAPS fixes:  {capsCount:N0}");
        Console.WriteLine();

        if (renames.Count == 0)
        {
            Console.WriteLine("Nothing to rename.");
            Pause();
            return;
        }

        Console.WriteLine("Samples:");

        foreach (var rename in renames.Take(12))
        {
            Console.WriteLine($"    '{rename.OldName}' -> '{rename.NewName}'");
        }

        Console.WriteLine();
        Console.WriteLine("Applying...");

        var rows = repository.UpdateArtistRenameBatch(
            renames.Select(r => (r.OldName, r.NewName)).ToList());

        Console.WriteLine();

        Console.WriteLine("Fixing folder casing on disk...");

        var organizationService =
            new OrganizationService(_database, repository);

        var foldersFixed = organizationService.FixDirectoryCasing(
            GetOrganizedRoot());

        Console.WriteLine();
        Console.WriteLine($"Rows updated:   {rows:N0}");
        Console.WriteLine($"Folders re-cased: {foldersFixed:N0}");
        Console.WriteLine(
            "Run 9/10 to move merged variants into their canonical folders.");

        Pause();
    }


    private void CleanDirtyArtistTags()
    {
        var repository =
            new LibraryFileRepository(_database);

        Console.WriteLine("Scanning artist tags for junk and decoration...");

        var cleanupService =
            new ArtistTagCleanupService(
                repository.GetKnownArtists());

        var rows = repository.GetArtistCleanupRows();

        var updates = new List<(long Id, string Artist, string AlbumArtist, string? Title)>();

        var reasons = new Dictionary<string, int>();

        var samples = new List<string>();

        foreach (var row in rows)
        {
            var cleaned = cleanupService.Clean(
                row.Id,
                row.FileName ?? "",
                row.Artist ?? "",
                row.AlbumArtist ?? "");

            if (cleaned == null)
            {
                continue;
            }

            updates.Add((
                cleaned.Id,
                cleaned.Artist ?? "",
                cleaned.AlbumArtist ?? "",
                cleaned.Title));

            reasons[cleaned.Reason] =
                reasons.GetValueOrDefault(cleaned.Reason) + 1;

            if (samples.Count < 12)
            {
                samples.Add(
                    $"    '{row.Artist}' -> '{cleaned.Artist ?? "(blanked)"}'");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Rows checked:  {rows.Count:N0}");
        Console.WriteLine($"Rows to clean: {updates.Count:N0}");

        foreach (var (reason, count) in reasons.OrderByDescending(r => r.Value))
        {
            Console.WriteLine($"    {reason}: {count:N0}");
        }

        Console.WriteLine();

        if (updates.Count == 0)
        {
            Console.WriteLine("Nothing to clean.");
            Pause();
            return;
        }

        Console.WriteLine("Samples:");

        foreach (var sample in samples)
        {
            Console.WriteLine(sample);
        }

        Console.WriteLine();
        Console.WriteLine("Applying...");

        repository.UpdateArtistCleanupBatch(updates);

        Console.WriteLine();
        Console.WriteLine(
            $"Cleaned {updates.Count:N0} row(s). Run 12-4 to re-match, " +
            "then 9/10 to re-organize.");

        Pause();
    }


    private void MatchArtistsFromDictionary()
    {
        var repository =
            new LibraryFileRepository(_database);

        Console.WriteLine("Loading artist dictionary from the library...");

        var matcher =
            new ArtistDictionaryMatchService(
                repository.GetKnownArtists());

        Console.WriteLine($"Known artists: {matcher.DictionarySize:N0}");
        Console.WriteLine();

        var files = repository.GetFilesForFilenameParsing();

        var updates = new List<(long Id, string Artist, string Title)>();

        var samples = new List<ArtistDictionaryMatch>();

        foreach (var file in files)
        {
            if (!string.IsNullOrEmpty(file.Artist))
            {
                continue;
            }

            var match = matcher.Match(
                file.Id,
                file.FileName ?? "",
                file.Title ?? "");

            if (match == null)
            {
                continue;
            }

            updates.Add((match.Id, match.Artist, match.Title));

            if (samples.Count < 12)
            {
                samples.Add(match);
            }
        }

        Console.WriteLine($"Rows missing artist: {files.Count:N0}");
        Console.WriteLine($"Matched:             {updates.Count:N0}");
        Console.WriteLine();

        if (updates.Count == 0)
        {
            Console.WriteLine("No matches found. Nothing was changed.");
            Pause();
            return;
        }

        Console.WriteLine("Sample matches:");

        foreach (var sample in samples)
        {
            Console.WriteLine($"    {sample.FileName}");
            Console.WriteLine($"        artist: {sample.Artist} | title: {sample.Title}");
        }

        Console.WriteLine();
        Console.WriteLine("Applying (unmatched files are left untouched)...");

        repository.UpdateArtistTitleBatch(updates);

        Console.WriteLine();
        Console.WriteLine(
            $"Matched artist on {updates.Count:N0} row(s) at 70% confidence. " +
            "Run options 9/10 to organize them.");

        Pause();
    }


    private void FillMetadataFromFilenames()
    {
        var repository =
            new LibraryFileRepository(_database);

        var parser = new FilenameParsingService();

        Console.WriteLine("Parsing filenames for rows missing Artist/Title...");
        Console.WriteLine();

        var files = repository.GetFilesForFilenameParsing();

        var updates = new List<(long Id, string? Artist, string? Title, int? TrackNumber)>();

        var samples = new List<(string FileName, string? Artist, string? Title)>();

        foreach (var file in files)
        {
            var parsed = parser.Parse(file.FileName ?? "");

            var fillsArtist =
                string.IsNullOrEmpty(file.Artist) && parsed.Artist != null;

            var fillsTitle =
                string.IsNullOrEmpty(file.Title) && parsed.Title != null;

            if (!fillsArtist && !fillsTitle)
            {
                continue;
            }

            updates.Add((file.Id, parsed.Artist, parsed.Title, parsed.TrackNumber));

            if (samples.Count < 10)
            {
                samples.Add((file.FileName ?? "", parsed.Artist, parsed.Title));
            }
        }

        Console.WriteLine($"Rows missing Artist/Title: {files.Count:N0}");
        Console.WriteLine($"Rows parseable:            {updates.Count:N0}");
        Console.WriteLine();

        if (updates.Count == 0)
        {
            Pause();
            return;
        }

        Console.WriteLine("Sample parses:");

        foreach (var (fileName, artist, title) in samples)
        {
            Console.WriteLine($"    {fileName}");
            Console.WriteLine($"        artist: {artist ?? "(unchanged)"} | title: {title ?? "(unchanged)"}");
        }

        Console.WriteLine();
        Console.WriteLine(
            "Applying (blanks only - existing values are never overwritten)...");

        repository.UpdateParsedMetadataBatch(updates);

        Console.WriteLine();
        Console.WriteLine($"Filled metadata on {updates.Count:N0} row(s) at 70% confidence.");
        Console.WriteLine();
        Console.WriteLine("Refreshing metadata gap issues...");

        var analyzer = new MetadataAnalyzer(_database);

        var report = analyzer.FindMissingMetadata();

        Console.WriteLine(
            $"Remaining files with metadata gaps: {report.TotalFilesWithGaps:N0}");

        Pause();
    }


    private void PurgeDeadRows()
    {
        var repository =
            new LibraryFileRepository(_database);

        var deadRowIds = repository.GetDeadRowIds();

        if (deadRowIds.Count == 0)
        {
            Console.WriteLine("No dead rows to purge.");
            Pause();
            return;
        }

        Console.WriteLine(
            $"Rows whose file is confirmed gone: {deadRowIds.Count:N0}");

        Console.WriteLine(
            "These database rows (and their issues, duplicate links and " +
            "plans) will be deleted. Audio files are not touched.");

        Console.WriteLine(
            "The database will be backed up first.");

        if (_settings.RequireDeleteConfirmation)
        {
            Console.WriteLine();

            Console.Write("Type YES to proceed: ");

            var confirmation = Console.ReadLine();

            if (!string.Equals(confirmation?.Trim(), "YES", StringComparison.Ordinal))
            {
                Console.WriteLine();
                Console.WriteLine("Purge cancelled. No changes were made.");

                Pause();
                return;
            }
        }

        var cleanupService =
            new StaleEntryCleanupService(
                repository,
                _settings.DatabasePath);

        Console.WriteLine();
        Console.WriteLine("Backing up database...");

        var backupPath = cleanupService.BackupDatabase(
            GetBackupsDirectory(), _settings.BackupRetention);

        Console.WriteLine($"    {backupPath}");
        Console.WriteLine();

        var deleted = repository.DeleteBatch(deadRowIds);

        Console.WriteLine($"Rows deleted: {deleted:N0}");

        Pause();
    }


    private void ExportCorruptFileList()
    {
        var repository =
            new LibraryFileRepository(_database);

        var corruptFiles = repository.GetCorruptFiles();

        if (corruptFiles.Count == 0)
        {
            Console.WriteLine("No corrupt files recorded.");
            Pause();
            return;
        }

        var reportsDirectory = Path.Combine(GetProjectRoot(), "Reports");

        Directory.CreateDirectory(reportsDirectory);

        var reportPath = Path.Combine(
            reportsDirectory,
            $"CorruptFiles_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var builder = new System.Text.StringBuilder();

        builder.AppendLine("FilePath,Error");

        foreach (var (filePath, error) in corruptFiles)
        {
            builder.Append('"').Append(filePath.Replace("\"", "\"\"")).Append('"');
            builder.Append(',');
            builder.Append('"').Append(error.Replace("\"", "\"\"")).Append('"');
            builder.AppendLine();
        }

        File.WriteAllText(reportPath, builder.ToString());

        Console.WriteLine($"Exported {corruptFiles.Count:N0} corrupt file(s) to:");
        Console.WriteLine($"    {reportPath}");

        Pause();
    }


    private void EnrichYearGenre()
    {
        var repository =
            new LibraryFileRepository(_database);

        using var httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "TidyTunes/1.0 (personal music library tool)");

        var client =
            new MusicBrainzClient(httpClient);

        var enrichmentService =
            new MusicBrainzEnrichmentService(
                repository,
                client);

        enrichmentService.RunAsync().GetAwaiter().GetResult();

        Pause();
    }


    private void IdentifyTracks()
    {
        if (string.IsNullOrWhiteSpace(_settings.AcoustIdApiKey))
        {
            Console.WriteLine(
                "AcoustIdApiKey is not set in settings.json. " +
                "Get a free key at https://acoustid.org/api-key");

            Pause();
            return;
        }

        var fingerprintService =
            new FingerprintService(_settings.FpCalcPath);

        if (!fingerprintService.IsAvailable(out var resolvedPath))
        {
            Console.WriteLine(
                $"fpcalc not found at '{_settings.FpCalcPath}'. " +
                "Download it from https://acoustid.org/chromaprint " +
                "and set FpCalcPath in settings.json.");

            Console.WriteLine($"(Looked for: {resolvedPath})");

            Pause();
            return;
        }

        var repository =
            new LibraryFileRepository(_database);

        using var httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "TidyTunes/1.0 (personal music library tool)");

        var acoustIdClient =
            new AcoustIdClient(httpClient, _settings.AcoustIdApiKey);

        var identificationService =
            new IdentificationService(
                repository,
                fingerprintService,
                acoustIdClient);

        identificationService.RunAsync().GetAwaiter().GetResult();

        Pause();
    }


    private void FindMissingMetadata()
    {
        Console.WriteLine(
            "Scanning for missing metadata...");

        var analyzer =
            new MetadataAnalyzer(_database);

        var report =
            analyzer.FindMissingMetadata();

        Console.WriteLine();

        if (report.TotalFilesWithGaps == 0)
        {
            Console.WriteLine(
                "No missing metadata found. Every file has Artist, Album, and Title.");

            Pause();
            return;
        }

        Console.WriteLine($"Files with at least one gap: {report.TotalFilesWithGaps:N0}");
        Console.WriteLine();

        Console.WriteLine($"Missing Artist: {report.MissingArtistCount:N0}");
        Console.WriteLine($"Missing Album:  {report.MissingAlbumCount:N0}");
        Console.WriteLine($"Missing Title:  {report.MissingTitleCount:N0}");
        Console.WriteLine();

        Console.WriteLine(
            $"Missing everything (Artist + Album + Title): {report.MissingAllCount:N0}");
        Console.WriteLine();

        if (report.WorstOffenders.Count > 0)
        {
            var previewCount = Math.Min(report.WorstOffenders.Count, 5);

            Console.WriteLine($"Showing {previewCount} of the worst offenders:");
            Console.WriteLine();

            for (var i = 0; i < previewCount; i++)
            {
                Console.WriteLine($"    {report.WorstOffenders[i].FilePath}");
            }

            Console.WriteLine();
        }

        Console.WriteLine(
            "Each gap has been recorded in the Issues table " +
            "for review under Cleanup > Review Issues.");

        Pause();
    }


    private void FindDuplicates()
    {
        Console.WriteLine(
            "Scanning for exact duplicates (SHA256)...");

        var analyzer =
            new DuplicateAnalyzer(_database);

        var groups =
            analyzer.FindExactDuplicates();

        Console.WriteLine();

        if (groups.Count == 0)
        {
            Console.WriteLine(
                "No exact duplicates found. " +
                "(If you haven't run Database Maintenance yet, " +
                "files may still be missing SHA256 hashes.)");

            Pause();
            return;
        }

        var totalDuplicateFiles = groups.Sum(g => g.Files.Count);
        var totalWastedBytes = groups.Sum(g => g.WastedBytes);

        Console.WriteLine($"Duplicate groups found: {groups.Count:N0}");
        Console.WriteLine($"Files involved:          {totalDuplicateFiles:N0}");
        Console.WriteLine($"Wasted space:            {FormatBytes(totalWastedBytes)}");
        Console.WriteLine();

        var previewCount = Math.Min(groups.Count, 5);

        Console.WriteLine($"Showing first {previewCount} group(s):");
        Console.WriteLine();

        for (var i = 0; i < previewCount; i++)
        {
            var group = groups[i];

            Console.WriteLine(
                $"Group {i + 1}: {group.Files.Count} copies, " +
                $"{FormatBytes(group.FileSize)} each " +
                $"({FormatBytes(group.WastedBytes)} wasted)");

            foreach (var file in group.Files)
            {
                Console.WriteLine($"    {file.FilePath}");
            }

            Console.WriteLine();
        }

        if (groups.Count > previewCount)
        {
            Console.WriteLine(
                $"...and {groups.Count - previewCount:N0} more group(s). " +
                "Full results are saved in the DuplicateGroups / DuplicateMatches tables.");
        }

        Pause();
    }


    private static string FormatBytes(long bytes)
    {
        double size = bytes;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:N1} {units[unitIndex]}";
    }


    private void RunDatabaseMaintenance()
    {
        var repository =
            new LibraryFileRepository(_database);

        var maintenanceService =
            new HashMaintenanceService(repository);

        maintenanceService.ProcessMissingHashes();

        Pause();
    }


    private void ShowDatabaseStatus()
    {
        var inspector =
            new DatabaseInspector(_database);

        inspector.ShowStatus();

        Pause();
    }


    // ---- Dashboard shown above the menu ----

    private void ShowDashboard()
    {
        try
        {
            var repository = new LibraryFileRepository(_database);

            var total = repository.GetCount();

            // Distinct primary artists: featuring credits collapse to
            // the lead artist, spelling variants fold together.
            var primaries = new HashSet<string>(StringComparer.Ordinal);

            var featSplit = new System.Text.RegularExpressions.Regex(
                @"\s+(feat\.?|featuring|ft\.?|f\.?|vs\.?|versus|meets)\s+|,|\s&\s",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (var (artist, _) in repository.GetKnownArtists())
            {
                var primary = featSplit.Split(artist)[0].Trim();

                var key = TidyTunes.Core.ArtistNameFolding.FoldKey(primary);

                if (key.Length > 0)
                {
                    primaries.Add(key);
                }
            }

            var backupsDir = GetBackupsDirectory();

            DateTime? lastBackup = null;

            if (Directory.Exists(backupsDir))
            {
                lastBackup = Directory
                    .EnumerateFiles(backupsDir)
                    .Select(File.GetLastWriteTime)
                    .OrderDescending()
                    .Cast<DateTime?>()
                    .FirstOrDefault();
            }

            var nextDue = (lastBackup ?? DateTime.Now).AddDays(30);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                $"  Songs: {total:N0}   Artists (primary, ft. excluded): {primaries.Count:N0}   " +
                $"Last backup: {(lastBackup?.ToString("yyyy-MM-dd") ?? "never")}   " +
                $"Next maintenance due: {nextDue:yyyy-MM-dd}  (reminder: Settings > M)");
            Console.ResetColor();
        }
        catch (Exception)
        {
            // The dashboard must never block the menu.
        }
    }


    private string GetBackupsDirectory()
    {
        return string.IsNullOrWhiteSpace(_settings.BackupsPath)
            ? Path.Combine(GetProjectRoot(), "Data", "Backups")
            : _settings.BackupsPath;
    }


    // ---- Search & Filter (option 6) ----

    private void SearchAndFilter()
    {
        var repository = new LibraryFileRepository(_database);

        Console.WriteLine("Search & Filter");
        Console.WriteLine("---------------");
        Console.WriteLine("1. Search by field (artist, album, title, genre, year, filename, folder, hash, MusicBrainz ID)");
        Console.WriteLine("2. Filter: corrupt files");
        Console.WriteLine("3. Filter: missing artist or title");
        Console.WriteLine("4. Filter: missing genre");
        Console.WriteLine("5. Filter: missing year");
        Console.WriteLine("6. Filter: low bitrate (under 160 kbps)");
        Console.WriteLine("7. Filter: not yet identified (no MusicBrainz match)");
        Console.WriteLine("8. Filter: added in the last 30 days");
        Console.WriteLine("9. Filter: outside the organized library");
        Console.WriteLine("Q. Back");
        Console.WriteLine();
        Console.Write("Select: ");

        var choice = Console.ReadLine()?.Trim();

        Console.WriteLine();

        string? where = null;
        object[] args = Array.Empty<object>();

        switch (choice)
        {
            case "1":
                Console.Write("Field (artist/album/title/genre/year/filename/folder/hash/mbid or 'any'): ");
                var field = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "any";

                Console.Write("Search text: ");
                var term = Console.ReadLine()?.Trim() ?? "";

                if (term.Length == 0)
                {
                    return;
                }

                var like = $"%{term}%";

                (where, args) = field switch
                {
                    "artist" => ("(Artist LIKE $p0 OR AlbumArtist LIKE $p0)", new object[] { like }),
                    "album" => ("Album LIKE $p0", new object[] { like }),
                    "title" => ("Title LIKE $p0", new object[] { like }),
                    "genre" => ("Genre LIKE $p0", new object[] { like }),
                    "year" => ("Year = $p0", new object[] { term }),
                    "filename" => ("FileName LIKE $p0", new object[] { like }),
                    "folder" => ("FilePath LIKE $p0", new object[] { like }),
                    "hash" => ("(Md5Hash = $p0 OR Sha256Hash = $p0)", new object[] { term }),
                    "mbid" => ("(MusicBrainzRecordingId = $p0 OR AcoustIdId = $p0)", new object[] { term }),
                    _ => ("(Artist LIKE $p0 OR Album LIKE $p0 OR Title LIKE $p0 OR Genre LIKE $p0 OR FileName LIKE $p0)",
                          new object[] { like }),
                };
                break;

            case "2": where = "IntegrityStatus = 'Failed'"; break;
            case "3": where = "(Artist IS NULL OR Artist = '' OR Title IS NULL OR Title = '')"; break;
            case "4": where = "(Genre IS NULL OR Genre = '')"; break;
            case "5": where = "(Year IS NULL OR Year = 0)"; break;
            case "6": where = "BitRate > 0 AND BitRate < 160"; break;
            case "7": where = "(MusicBrainzRecordingId IS NULL OR MusicBrainzRecordingId = '')"; break;
            case "8":
                where = "DateAdded >= $p0";
                args = new object[] { DateTime.Now.AddDays(-30).ToString("o") };
                break;
            case "9":
                where = "FilePath NOT LIKE $p0 AND FilePath NOT LIKE $p1";
                args = new object[] { GetOrganizedRoot() + "\\%", GetEffectsRoot() + "\\%" };
                break;

            default:
                return;
        }

        var count = repository.CountFiles(where, args);

        var rows = repository.QueryFiles(where, args, 40);

        Console.WriteLine($"Matches: {count:N0}  (showing first {rows.Count})");
        Console.WriteLine();

        foreach (var r in rows)
        {
            var meta = $"{r.Artist} - {r.Title}".Trim(' ', '-');

            if (meta.Length == 0)
            {
                meta = r.FileName ?? "";
            }

            var extra = $"{(r.Year > 0 ? r.Year + " " : "")}{r.Genre}".Trim();

            Console.WriteLine($"  {meta}" + (extra.Length > 0 ? $"  [{extra}]" : ""));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"      {r.FilePath}");
            Console.ResetColor();
        }

        if (count > rows.Count)
        {
            Console.WriteLine($"  ... and {count - rows.Count:N0} more.");
        }

        Console.WriteLine();
        Console.Write("Export ALL matches to CSV in Reports? (y/N): ");

        if (string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
        {
            var all = repository.QueryFiles(where, args);

            var path = WriteCsv("Search", all);

            Console.WriteLine($"Exported {all.Count:N0} row(s) to {path}");
        }

        Pause();
    }


    private string GetOrganizedRoot()
    {
        return string.IsNullOrWhiteSpace(_settings.OrganizedRoot)
            ? Path.Combine(_settings.MusicLibrary, "Organized")
            : _settings.OrganizedRoot;
    }


    private string GetEffectsRoot()
    {
        return string.IsNullOrWhiteSpace(_settings.EffectsRoot)
            ? Path.Combine(Path.GetPathRoot(_settings.MusicLibrary) ?? @"D:\", "Effects")
            : _settings.EffectsRoot;
    }


    private string WriteCsv(string prefix, List<LibraryFile> rows)
    {
        var reportsDir = Path.Combine(GetProjectRoot(), "Reports");

        Directory.CreateDirectory(reportsDir);

        var path = Path.Combine(
            reportsDir, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("Artist,Title,Album,Genre,Year,BitRate,DurationSeconds,Bpm,Key,Integrity,FilePath");

        static string F(string? v) => $"\"{(v ?? "").Replace("\"", "\"\"")}\"";

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                F(r.Artist), F(r.Title), F(r.Album), F(r.Genre),
                r.Year ?? 0, r.BitRate ?? 0, r.DurationSeconds ?? 0,
                r.Bpm ?? 0, F(r.MusicalKey), F(r.IntegrityStatus), F(r.FilePath)));
        }

        File.WriteAllText(path, sb.ToString());

        return path;
    }


    // ---- Audio Duplicates Report (option 8) ----

    private void AudioDuplicatesReport()
    {
        var repository = new LibraryFileRepository(_database);

        var resolutionService = new DuplicateResolutionService(_database, repository);

        Console.WriteLine("Analyzing same-recording duplicates (report only - nothing moves)...");
        Console.WriteLine();

        var report = resolutionService.AnalyzeAudioDuplicates();

        Console.WriteLine($"Recordings with multiple copies: {report.Groups:N0}");
        Console.WriteLine($"Surplus copies:                  {report.SurplusFiles:N0}");
        Console.WriteLine($"Space they occupy:               {FormatBytes(report.SurplusBytes)}");
        Console.WriteLine();

        foreach (var line in report.SampleLines)
        {
            Console.WriteLine("  " + line);
        }

        Console.WriteLine();
        Console.WriteLine("Use option 16 (Resolve Duplicates) to quarantine the extras.");

        Pause();
    }


    // ---- Quality Issues Report (option 10) ----

    private void QualityIssuesReport()
    {
        var repository = new LibraryFileRepository(_database);

        Console.WriteLine("Scanning for quality issues...");
        Console.WriteLine();

        var checks = new (string Type, string Where, string Label)[]
        {
            ("LowBitrate", "BitRate > 0 AND BitRate < 128 AND (QuarantinedDate IS NULL OR QuarantinedDate='')", "Very low bitrate (< 128 kbps)"),
            ("ModestBitrate", "BitRate >= 128 AND BitRate < 192 AND (QuarantinedDate IS NULL OR QuarantinedDate='')", "Modest bitrate (128-191 kbps)"),
            ("LowSampleRate", "SampleRate > 0 AND SampleRate < 44100", "Sample rate below 44.1 kHz"),
            ("MonoAudio", "Channels = 1 AND DurationSeconds >= 60", "Mono recordings (full-length)"),
            ("ZeroBytes", "FileSize = 0", "Zero-byte files"),
            ("SuspectFlac", "LOWER(Extension) = '.flac' AND BitRate > 0 AND BitRate < 500", "Suspect FLACs (bitrate looks transcoded)"),
            ("CorruptAudio", "IntegrityStatus = 'Failed'", "Corrupt (failed decode test)"),
        };

        foreach (var (type, where, label) in checks)
        {
            var count = repository.CountFiles(where, Array.Empty<object>());

            Console.WriteLine($"  {label}: {count:N0}");

            if (count > 0 && count <= 3)
            {
                foreach (var r in repository.QueryFiles(where, Array.Empty<object>(), 3))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      {r.FilePath}");
                    Console.ResetColor();
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("Notes: transcode detection and clipping analysis need a deep");
        Console.WriteLine("audio scan and are planned as a future feature. Use option 21");
        Console.WriteLine("to export any of these lists as CSV.");

        Pause();
    }


    // ---- Library Report (option 20) ----

    private void GenerateLibraryReport()
    {
        var repository = new LibraryFileRepository(_database);

        Console.WriteLine("Generating library report...");

        var stats = repository.GetStatistics();

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("TidyTunes Library Report");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();
        sb.AppendLine($"Total files:      {stats.TotalFiles:N0}");
        sb.AppendLine($"Distinct artists: {stats.ArtistCount:N0}");
        sb.AppendLine($"Distinct albums:  {stats.AlbumCount:N0}");
        sb.AppendLine();

        sb.AppendLine("File types:");
        foreach (var (ext, n) in stats.FileTypeCounts)
        {
            sb.AppendLine($"  {ext}: {n:N0}");
        }

        sb.AppendLine();
        sb.AppendLine("Metadata health:");
        sb.AppendLine($"  Missing artist: {stats.MissingArtist:N0}");
        sb.AppendLine($"  Missing album:  {stats.MissingAlbum:N0}");
        sb.AppendLine($"  Missing title:  {stats.MissingTitle:N0}");
        sb.AppendLine($"  Hashes:         {stats.Md5Complete:N0} MD5 / {stats.Sha256Complete:N0} SHA-256");
        sb.AppendLine($"  Open issues:    {stats.Issues:N0}");
        sb.AppendLine();

        foreach (var (label, sql) in new (string, string)[]
        {
            ("Top 25 genres", "SELECT Genre, COUNT(*) FROM LibraryFiles WHERE Genre != '' GROUP BY Genre ORDER BY 2 DESC LIMIT 25"),
            ("Files by decade", "SELECT (Year/10)*10 || 's', COUNT(*) FROM LibraryFiles WHERE Year > 0 GROUP BY Year/10 ORDER BY 1"),
            ("Top 25 artists by track count", "SELECT Artist, COUNT(*) FROM LibraryFiles WHERE Artist != '' GROUP BY Artist ORDER BY 2 DESC LIMIT 25"),
            ("Integrity", "SELECT COALESCE(NULLIF(IntegrityStatus,''),'Unchecked'), COUNT(*) FROM LibraryFiles GROUP BY 1 ORDER BY 2 DESC"),
        })
        {
            sb.AppendLine($"{label}:");

            using var connection = _database.GetConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                sb.AppendLine($"  {reader.GetValue(0)}: {reader.GetInt64(1):N0}");
            }

            sb.AppendLine();
        }

        var reportsDir = Path.Combine(GetProjectRoot(), "Reports");
        Directory.CreateDirectory(reportsDir);

        var path = Path.Combine(reportsDir, $"LibraryReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllText(path, sb.ToString());

        Console.WriteLine($"Report written to:");
        Console.WriteLine($"    {path}");

        Pause();
    }


    // ---- Export Results (option 21) ----

    private void ExportResults()
    {
        var repository = new LibraryFileRepository(_database);

        Console.WriteLine("Export Results");
        Console.WriteLine("--------------");
        Console.WriteLine("1. Corrupt files (re-download list)");
        Console.WriteLine("2. Full library inventory");
        Console.WriteLine("3. Files missing metadata");
        Console.WriteLine("4. Low-bitrate files (< 192 kbps)");
        Console.WriteLine("5. Unorganized files");
        Console.WriteLine("Q. Back");
        Console.WriteLine();
        Console.Write("Select: ");

        var choice = Console.ReadLine()?.Trim();

        Console.WriteLine();

        (string Where, object[] Args, string Name)? pick = choice switch
        {
            "1" => ("IntegrityStatus = 'Failed'", Array.Empty<object>(), "CorruptFiles"),
            "2" => ("1=1", Array.Empty<object>(), "FullInventory"),
            "3" => ("(Artist='' OR Title='' OR Genre='' OR Year IS NULL OR Year=0)", Array.Empty<object>(), "MissingMetadata"),
            "4" => ("BitRate > 0 AND BitRate < 192", Array.Empty<object>(), "LowBitrate"),
            "5" => ("FilePath NOT LIKE $p0 AND FilePath NOT LIKE $p1",
                    new object[] { GetOrganizedRoot() + "\\%", GetEffectsRoot() + "\\%" }, "Unorganized"),
            _ => null,
        };

        if (pick == null)
        {
            return;
        }

        var rows = repository.QueryFiles(pick.Value.Where, pick.Value.Args);

        var path = WriteCsv(pick.Value.Name, rows);

        Console.WriteLine($"Exported {rows.Count:N0} row(s) to:");
        Console.WriteLine($"    {path}");

        Pause();
    }


    // ---- Settings editor (option 23) ----

    private void EditSettings()
    {
        while (true)
        {
            var entries = new (string Label, Func<string> Get, Action<string> Set)[]
            {
                ("Music library root", () => _settings.MusicLibrary, v => _settings.MusicLibrary = v),
                ("Organized destination", GetOrganizedRoot, v => _settings.OrganizedRoot = v),
                ("Effects destination", GetEffectsRoot, v => _settings.EffectsRoot = v),
                ("Organization template", () => _settings.OrganizationTemplate, v => _settings.OrganizationTemplate = v),
                ("Database path", () => _settings.DatabasePath, v => _settings.DatabasePath = v),
                ("Backups folder", GetBackupsDirectory, v => _settings.BackupsPath = v),
                ("Backup retention (0 = unlimited)", () => _settings.BackupRetention.ToString(),
                    v => _settings.BackupRetention = int.TryParse(v, out var n) ? n : _settings.BackupRetention),
                ("AcoustID API key", () => _settings.AcoustIdApiKey, v => _settings.AcoustIdApiKey = v),
                ("fpcalc path", () => _settings.FpCalcPath, v => _settings.FpCalcPath = v),
                ("ffmpeg path", () => _settings.FfmpegPath, v => _settings.FfmpegPath = v),
                ("Require delete confirmation", () => _settings.RequireDeleteConfirmation.ToString(),
                    v => _settings.RequireDeleteConfirmation = !v.Trim().ToLowerInvariant().StartsWith("f")),
            };

            Console.WriteLine("Settings  (number to edit, M for maintenance reminder, Q to save & back)");
            Console.WriteLine("--------");

            for (var i = 0; i < entries.Length; i++)
            {
                Console.WriteLine($"{i + 1,3}. {entries[i].Label,-34} {entries[i].Get()}");
            }

            Console.WriteLine("  M. Create maintenance reminder (.ics calendar file)");
            Console.WriteLine();
            Console.Write("Select: ");

            var choice = Console.ReadLine()?.Trim();

            Console.WriteLine();

            if (string.Equals(choice, "q", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(choice))
            {
                SaveSettings();
                Console.WriteLine("Settings saved.");
                Pause();
                return;
            }

            if (string.Equals(choice, "m", StringComparison.OrdinalIgnoreCase))
            {
                CreateMaintenanceReminder();
                continue;
            }

            if (int.TryParse(choice, out var index)
                && index >= 1 && index <= entries.Length)
            {
                Console.WriteLine($"Current: {entries[index - 1].Get()}");
                Console.Write("New value (ENTER keeps current): ");

                var value = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    entries[index - 1].Set(value.Trim());
                }
            }

            Console.WriteLine();
        }
    }


    private void SaveSettings()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                _settings,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not save settings: {ex.Message}");
        }
    }


    // Writes an RFC 5545 .ics calendar file (opens in Outlook, Apple
    // Calendar, Google Calendar - attach it to an email to yourself).
    private void CreateMaintenanceReminder()
    {
        var due = DateTime.Now.AddDays(30).Date.AddHours(10);

        Console.Write($"Reminder date/time [{due:yyyy-MM-dd HH:mm}] (ENTER accepts): ");

        var input = Console.ReadLine();

        if (DateTime.TryParse(input, out var custom))
        {
            due = custom;
        }

        var ics = string.Join("\r\n", new[]
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//TidyTunes//Maintenance//EN",
            "BEGIN:VEVENT",
            $"UID:tidytunes-{Guid.NewGuid()}",
            $"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}",
            $"DTSTART:{due:yyyyMMdd'T'HHmmss}",
            $"DTEND:{due.AddHours(1):yyyyMMdd'T'HHmmss}",
            "SUMMARY:TidyTunes library maintenance",
            "DESCRIPTION:Run TidyTunes: 1) Scan for new music 2) Identify " +
                "new tracks 3) Verify integrity 4) Resolve duplicates 5) Apply organization.",
            "BEGIN:VALARM",
            "TRIGGER:-PT1H",
            "ACTION:DISPLAY",
            "DESCRIPTION:TidyTunes maintenance due",
            "END:VALARM",
            "END:VEVENT",
            "END:VCALENDAR",
            "",
        });

        var reportsDir = Path.Combine(GetProjectRoot(), "Reports");

        Directory.CreateDirectory(reportsDir);

        var path = Path.Combine(
            reportsDir, $"TidyTunes_Maintenance_{due:yyyyMMdd}.ics");

        File.WriteAllText(path, ics);

        Console.WriteLine($"Calendar file written to:");
        Console.WriteLine($"    {path}");
        Console.WriteLine("Email it to yourself or double-click to add to your calendar.");
        Console.WriteLine();
    }


    private void Pause()
    {
        Console.WriteLine();
        Console.WriteLine(
            "Press ENTER to continue.");

        Console.ReadLine();
    }
}