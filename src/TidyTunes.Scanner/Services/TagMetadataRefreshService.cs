using System.Diagnostics;
using TidyTunes.Core.Models;
using TidyTunes.Data.Repositories;

namespace TidyTunes.Scanner.Services;

// Re-reads embedded tags for every live file and fills database
// blanks with what the tags contain - including the DJ fields
// (BPM, initial key, composer) and MusicBrainz IDs that older scans
// never extracted. Never overwrites existing database values, runs
// tag reads in parallel, and commits in batches, unlike the original
// per-file MetadataRefreshService.
public class TagMetadataRefreshService
{
    private readonly LibraryFileRepository _repository;
    private readonly MetadataExtractor _extractor;

    private const int BatchSize = 500;


    public TagMetadataRefreshService(
        LibraryFileRepository repository,
        MetadataExtractor extractor)
    {
        _repository = repository;
        _extractor = extractor;
    }


    public void Run()
    {
        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine("Tag Metadata Refresh (fill-only)");
        Console.WriteLine("=================================");
        Console.WriteLine();


        var files = _repository.GetFilesForTagRefresh();

        if (files.Count == 0)
        {
            Console.WriteLine("No files to refresh.");
            Console.WriteLine();
            return;
        }


        var workerCount = Math.Clamp(
            Environment.ProcessorCount - 2, 2, 8);

        Console.WriteLine($"Files to read: {files.Count:N0}");
        Console.WriteLine($"Parallel readers: {workerCount}");
        Console.WriteLine();


        var processed = 0;
        var read = 0;
        var failed = 0;

        var pendingUpdates = new List<LibraryFile>(BatchSize);

        var pendingLock = new object();

        var stopwatch = Stopwatch.StartNew();

        var lastReportTime = TimeSpan.Zero;


        Parallel.ForEach(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = workerCount },
            file =>
            {
                LibraryFile? metadata = null;

                try
                {
                    if (File.Exists(file.FilePath))
                    {
                        metadata = _extractor.Extract(file.FilePath);

                        metadata.Id = file.Id;
                    }
                }
                catch (Exception)
                {
                    metadata = null;
                }

                if (metadata is null)
                {
                    Interlocked.Increment(ref failed);
                }
                else
                {
                    Interlocked.Increment(ref read);
                }

                var processedSoFar =
                    Interlocked.Increment(ref processed);


                lock (pendingLock)
                {
                    if (metadata is not null)
                    {
                        pendingUpdates.Add(metadata);

                        if (pendingUpdates.Count >= BatchSize)
                        {
                            _repository.UpdateTagMetadataBatch(pendingUpdates);
                            pendingUpdates.Clear();
                        }
                    }


                    var elapsed = stopwatch.Elapsed;

                    if (processedSoFar % 250 == 0 ||
                        elapsed - lastReportTime >= TimeSpan.FromSeconds(2) ||
                        processedSoFar == files.Count)
                    {
                        lastReportTime = elapsed;

                        var rate = processedSoFar / Math.Max(elapsed.TotalSeconds, 0.001);

                        var remaining = files.Count - processedSoFar;

                        var eta = rate > 0
                            ? TimeSpan.FromSeconds(remaining / rate)
                            : TimeSpan.Zero;

                        Console.WriteLine(
                            $"[{processedSoFar:N0}/{files.Count:N0}] " +
                            $"{rate:N1} files/sec, ETA {eta:hh\\:mm\\:ss}");
                    }
                }
            });


        lock (pendingLock)
        {
            if (pendingUpdates.Count > 0)
            {
                _repository.UpdateTagMetadataBatch(pendingUpdates);
                pendingUpdates.Clear();
            }
        }


        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine("Tag Metadata Refresh Complete");
        Console.WriteLine($"Files read:    {read:N0}");
        Console.WriteLine($"Read failures: {failed:N0}");
        Console.WriteLine($"Total time:    {stopwatch.Elapsed:hh\\:mm\\:ss}");
        Console.WriteLine("=================================");
    }
}
