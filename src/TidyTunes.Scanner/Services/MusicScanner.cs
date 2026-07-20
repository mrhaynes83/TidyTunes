using TidyTunes.Core.Models;
using TidyTunes.Data;
using TidyTunes.Data.Repositories;

namespace TidyTunes.Scanner.Services;

public class MusicScanner
{
    private readonly DatabaseService _database;
    private readonly LibraryFileRepository _repository;
    private readonly MetadataExtractor _metadataExtractor;
    private readonly FileHashService _hashService;


    private readonly string[] _extensions =
    {
        ".mp3",
        ".flac",
        ".wav",
        ".m4a",
        ".wma",
        ".ogg",
        ".aac",
        ".aiff",
        ".aif",
        ".ape",
        ".opus",
        ".mpc"
    };

    // DRM-locked formats: encrypted, unreadable by any tool outside
    // the store that sold them. Counted so they never silently vanish
    // from view, but they cannot be scanned, parsed or organized.
    private static readonly string[] DrmExtensions =
    {
        ".m4p",
        ".m4b"
    };

    public int SkippedDrmCount { get; private set; }


    public MusicScanner(DatabaseService database)
    {
        _database = database;

        _repository =
            new LibraryFileRepository(database);

        _metadataExtractor =
            new MetadataExtractor();

        _hashService =
            new FileHashService();
    }


    public int Scan(string folder)
    {
        Console.WriteLine();
        Console.WriteLine("Starting music scan...");
        Console.WriteLine(folder);
        Console.WriteLine();


        var allFiles =
            Directory.EnumerateFiles(
                folder,
                "*.*",
                SearchOption.AllDirectories)
            .ToList();

        var files = allFiles.Where(IsAudioFile).ToList();

        SkippedDrmCount = allFiles.Count(f =>
            DrmExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));


        Console.WriteLine(
            $"Found {files.Count:N0} audio files.");

        if (SkippedDrmCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                $"WARNING: {SkippedDrmCount:N0} DRM-protected file(s) (.m4p/.m4b) found. " +
                "These are encrypted and CANNOT be parsed or organized by TidyTunes " +
                "(or any tool outside an authorized iTunes). They will be skipped.");
            Console.ResetColor();
        }

        Console.WriteLine();


        var processed = 0;

        const int batchSize = 500;
        var pendingFiles = new List<LibraryFile>(batchSize);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastReportTime = TimeSpan.Zero;


        foreach (var file in files)
        {
            processed++;


            var libraryFile =
                _metadataExtractor.Extract(file);


            var hashes =
                _hashService.ComputeBoth(file);

            libraryFile.Md5Hash = hashes.Md5;
            libraryFile.Sha256Hash = hashes.Sha256;
            libraryFile.FileHash = hashes.Sha256;


            pendingFiles.Add(libraryFile);

            if (pendingFiles.Count >= batchSize)
            {
                _repository.AddOrUpdateBatch(pendingFiles);
                pendingFiles.Clear();
            }


            // Report progress every 100 files or every 2 seconds,
            // instead of printing multiple lines per file.

            var elapsed = stopwatch.Elapsed;

            if (processed % 100 == 0 ||
                elapsed - lastReportTime >= TimeSpan.FromSeconds(2) ||
                processed == files.Count)
            {
                lastReportTime = elapsed;

                var rate = processed / Math.Max(elapsed.TotalSeconds, 0.001);

                var remaining = files.Count - processed;

                var eta = rate > 0
                    ? TimeSpan.FromSeconds(remaining / rate)
                    : TimeSpan.Zero;

                Console.WriteLine(
                    $"[{processed:N0}/{files.Count:N0}] " +
                    $"{rate:N1} files/sec, ETA {eta:hh\\:mm\\:ss}");
            }
        }


        if (pendingFiles.Count > 0)
        {
            _repository.AddOrUpdateBatch(pendingFiles);
            pendingFiles.Clear();
        }


        Console.WriteLine();

        Console.WriteLine(
            $"Scan complete: {processed:N0} files processed in {stopwatch.Elapsed:hh\\:mm\\:ss}.");

        return processed;
    }


    private bool IsAudioFile(string file)
    {
        var extension =
            Path.GetExtension(file)
                .ToLowerInvariant();

        return _extensions.Contains(extension);
    }
}