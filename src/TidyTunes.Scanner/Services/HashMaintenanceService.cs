using TidyTunes.Data.Repositories;

namespace TidyTunes.Scanner.Services;

public class HashMaintenanceService
{
    private readonly LibraryFileRepository _repository;
    private readonly HashService _hashService;


    public HashMaintenanceService(
        LibraryFileRepository repository)
    {
        _repository = repository;
        _hashService = new HashService();
    }


    public void ProcessMissingHashes()
    {
        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine("Hash Generation");
        Console.WriteLine("=================================");
        Console.WriteLine();


        var files =
            _repository.GetMissingHashes();


        if (files.Count == 0)
        {
            Console.WriteLine(
                "All files already have hashes.");

            Console.WriteLine();
            return;
        }


        Console.WriteLine(
            $"Files requiring hashes: {files.Count:N0}");

        Console.WriteLine();


        var processed = 0;
        var successful = 0;
        var failed = 0;

        const int batchSize = 500;
        var pendingUpdates = new List<(long Id, string Md5, string Sha256)>(batchSize);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastReportTime = TimeSpan.Zero;


        foreach (var file in files)
        {
            processed++;


            try
            {
                if (string.IsNullOrWhiteSpace(file.FilePath))
                {
                    failed++;
                    continue;
                }


                if (!File.Exists(file.FilePath))
                {
                    failed++;
                    continue;
                }


                var hashes =
                    _hashService.CalculateBoth(
                        file.FilePath);


                pendingUpdates.Add((file.Id, hashes.Md5, hashes.Sha256));

                if (pendingUpdates.Count >= batchSize)
                {
                    _repository.UpdateHashesBatch(pendingUpdates);
                    pendingUpdates.Clear();
                }


                successful++;
            }
            catch (Exception ex)
            {
                failed++;

                Console.WriteLine(
                    $"[{processed:N0}/{files.Count:N0}] ERROR on {file.FileName}: {ex.Message}");
            }


            // Report progress every 250 files or every 2 seconds,
            // whichever comes first, instead of printing a line per
            // file. Printing 200k+ console lines is itself slow and
            // makes the real bottleneck (disk I/O) harder to see.

            var elapsed = stopwatch.Elapsed;

            if (processed % 250 == 0 ||
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


        if (pendingUpdates.Count > 0)
        {
            _repository.UpdateHashesBatch(pendingUpdates);
            pendingUpdates.Clear();
        }


        Console.WriteLine();

        Console.WriteLine(
            "=================================");

        Console.WriteLine(
            "Hash Generation Complete");

        Console.WriteLine(
            $"Processed:  {processed:N0}");

        Console.WriteLine(
            $"Successful: {successful:N0}");

        Console.WriteLine(
            $"Failed:     {failed:N0}");

        Console.WriteLine(
            $"Total time: {stopwatch.Elapsed:hh\\:mm\\:ss}");

        Console.WriteLine(
            "=================================");
    }
}