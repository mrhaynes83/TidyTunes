using System.Threading.Channels;
using TidyTunes.Core.Models;
using TidyTunes.Data.Repositories;

namespace TidyTunes.Scanner.Services;

public class IdentificationService
{
    private readonly LibraryFileRepository _repository;
    private readonly FingerprintService _fingerprintService;
    private readonly AcoustIdClient _acoustIdClient;

    // AcoustID's documented limit is 3 requests/second. 350ms gives a
    // small safety margin under that (333ms would be the exact limit).
    private static readonly TimeSpan RequestInterval = TimeSpan.FromMilliseconds(350);

    // A match below this score is treated as "no confident match"
    // rather than being written to the library.
    private const double MinimumConfidentScore = 0.5;

    public IdentificationService(
        LibraryFileRepository repository,
        FingerprintService fingerprintService,
        AcoustIdClient acoustIdClient)
    {
        _repository = repository;
        _fingerprintService = fingerprintService;
        _acoustIdClient = acoustIdClient;
    }


    // Fingerprints every pending live file and looks each print up
    // against AcoustID. Fingerprinting (CPU-bound, ~1s/file) runs on
    // parallel workers feeding a channel; the single consumer makes
    // the rate-limited API calls. This keeps the API saturated at its
    // 3 req/sec limit instead of serializing fpcalc and the network
    // wait - roughly a 4x throughput difference at library scale.
    public async Task RunAsync()
    {
        var files = _repository.GetFilesNeedingFingerprint();

        Console.WriteLine(
            $"Files needing fingerprint + identification: {files.Count:N0}");

        if (files.Count > 0)
        {
            var estimatedSeconds = files.Count * RequestInterval.TotalSeconds;

            Console.WriteLine(
                $"Estimated time at 3 requests/sec: {TimeSpan.FromSeconds(estimatedSeconds):d\\d\\ hh\\:mm\\:ss}");
        }

        Console.WriteLine();


        var processed = 0;
        var matched = 0;
        var noMatch = 0;
        var failed = 0;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastReportTime = TimeSpan.Zero;
        var lastRequestTime = DateTime.MinValue;


        var workerCount = Math.Clamp(
            Environment.ProcessorCount - 2, 2, 8);

        // Bounded so fingerprinting can't run unboundedly ahead of the
        // rate-limited consumer and hold thousands of prints in memory.
        var channel = Channel.CreateBounded<(LibraryFile File, double Duration, string Fingerprint)>(
            new BoundedChannelOptions(64)
            {
                SingleReader = true
            });


        var producer = Task.Run(() =>
        {
            try
            {
                Parallel.ForEach(
                    files,
                    new ParallelOptions { MaxDegreeOfParallelism = workerCount },
                    file =>
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(file.FilePath) ||
                                !File.Exists(file.FilePath))
                            {
                                Interlocked.Increment(ref failed);
                                Interlocked.Increment(ref processed);
                                return;
                            }

                            var fingerprintResult =
                                _fingerprintService.Generate(file.FilePath);

                            if (fingerprintResult is null)
                            {
                                Interlocked.Increment(ref failed);
                                Interlocked.Increment(ref processed);
                                return;
                            }

                            var (duration, fingerprint) = fingerprintResult.Value;

                            while (!channel.Writer.TryWrite((file, duration, fingerprint)))
                            {
                                channel.Writer.WaitToWriteAsync()
                                    .AsTask().GetAwaiter().GetResult();
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failed);
                            Interlocked.Increment(ref processed);

                            Console.WriteLine(
                                $"ERROR fingerprinting {file.FileName}: {ex.Message}");
                        }
                    });
            }
            finally
            {
                channel.Writer.Complete();
            }
        });


        await foreach (var (file, duration, fingerprint) in channel.Reader.ReadAllAsync())
        {
            try
            {
                // Respect AcoustID's rate limit before making the call.

                var sinceLastRequest = DateTime.UtcNow - lastRequestTime;

                if (sinceLastRequest < RequestInterval)
                {
                    await Task.Delay(RequestInterval - sinceLastRequest);
                }

                lastRequestTime = DateTime.UtcNow;

                var match = await _acoustIdClient.LookupAsync(duration, fingerprint);


                if (match is not null && match.Score >= MinimumConfidentScore)
                {
                    matched++;

                    _repository.UpdateIdentificationResult(
                        file.Id,
                        fingerprint,
                        match.AcoustIdId,
                        match.Score,
                        match.RecordingId,
                        match.Artist,
                        match.Album,
                        match.Title,
                        "Matched");
                }
                else
                {
                    noMatch++;

                    _repository.UpdateIdentificationResult(
                        file.Id,
                        fingerprint,
                        match?.AcoustIdId,
                        match?.Score ?? 0.0,
                        null,
                        null,
                        null,
                        null,
                        "NoMatch");
                }
            }
            catch (Exception ex)
            {
                failed++;

                Console.WriteLine(
                    $"ERROR identifying {file.FileName}: {ex.Message}");
            }

            processed++;


            var elapsed = stopwatch.Elapsed;

            if (processed % 25 == 0 ||
                elapsed - lastReportTime >= TimeSpan.FromSeconds(5) ||
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
                    $"matched {matched:N0}, no match {noMatch:N0}, failed {failed:N0} " +
                    $"| {rate:N2} files/sec, ETA {eta:d\\d\\ hh\\:mm\\:ss}");
            }
        }


        await producer;


        Console.WriteLine();

        Console.WriteLine(
            "=================================");

        Console.WriteLine(
            "Identification Complete");

        Console.WriteLine(
            $"Processed:  {processed:N0}");

        Console.WriteLine(
            $"Matched:    {matched:N0}");

        Console.WriteLine(
            $"No match:   {noMatch:N0}");

        Console.WriteLine(
            $"Failed:     {failed:N0}");

        Console.WriteLine(
            $"Total time: {stopwatch.Elapsed:d\\d\\ hh\\:mm\\:ss}");

        Console.WriteLine(
            "=================================");
    }
}
