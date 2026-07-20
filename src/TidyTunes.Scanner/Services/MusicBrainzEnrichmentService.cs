using System.Diagnostics;
using TidyTunes.Data.Repositories;

namespace TidyTunes.Scanner.Services;

// Fills missing Year and Genre from MusicBrainz for every row that
// has a recording id. Works through DISTINCT recording ids (an album
// of files shares one lookup) at MusicBrainz's 1 request/second
// limit, applying each result to all rows with that id, so re-runs
// skip anything already filled - the job is resumable by nature.
public class MusicBrainzEnrichmentService
{
    private readonly LibraryFileRepository _repository;
    private readonly MusicBrainzClient _client;

    private static readonly TimeSpan RequestInterval =
        TimeSpan.FromMilliseconds(1100);


    public MusicBrainzEnrichmentService(
        LibraryFileRepository repository,
        MusicBrainzClient client)
    {
        _repository = repository;
        _client = client;
    }


    public async Task RunAsync()
    {
        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine("MusicBrainz Year/Genre Enrichment");
        Console.WriteLine("=================================");
        Console.WriteLine();


        var recordingIds = _repository.GetRecordingIdsNeedingEnrichment();

        if (recordingIds.Count == 0)
        {
            Console.WriteLine(
                "Nothing to enrich - no identified rows are missing Year or Genre.");

            Console.WriteLine();
            return;
        }


        Console.WriteLine($"Recordings to look up: {recordingIds.Count:N0}");

        Console.WriteLine(
            $"Estimated time at 1 request/sec: " +
            $"{TimeSpan.FromSeconds(recordingIds.Count * RequestInterval.TotalSeconds):d\\d\\ hh\\:mm\\:ss}");

        Console.WriteLine();


        var processed = 0;
        var rowsUpdated = 0;
        var lookupFailed = 0;

        var stopwatch = Stopwatch.StartNew();
        var lastReportTime = TimeSpan.Zero;
        var lastRequestTime = DateTime.MinValue;


        foreach (var recordingId in recordingIds)
        {
            processed++;

            try
            {
                var sinceLastRequest = DateTime.UtcNow - lastRequestTime;

                if (sinceLastRequest < RequestInterval)
                {
                    await Task.Delay(RequestInterval - sinceLastRequest);
                }

                lastRequestTime = DateTime.UtcNow;

                var result = await _client.LookupRecordingAsync(recordingId);

                if (result is null)
                {
                    lookupFailed++;
                }
                else
                {
                    var (year, genre) = result.Value;

                    if (year != null || genre != null)
                    {
                        rowsUpdated += _repository.UpdateYearGenreByRecordingId(
                            recordingId,
                            year,
                            genre);
                    }
                }
            }
            catch (Exception ex)
            {
                lookupFailed++;

                Console.WriteLine(
                    $"ERROR on recording {recordingId}: {ex.Message}");
            }


            var elapsed = stopwatch.Elapsed;

            if (processed % 50 == 0 ||
                elapsed - lastReportTime >= TimeSpan.FromSeconds(5) ||
                processed == recordingIds.Count)
            {
                lastReportTime = elapsed;

                var rate = processed / Math.Max(elapsed.TotalSeconds, 0.001);

                var remaining = recordingIds.Count - processed;

                var eta = rate > 0
                    ? TimeSpan.FromSeconds(remaining / rate)
                    : TimeSpan.Zero;

                Console.WriteLine(
                    $"[{processed:N0}/{recordingIds.Count:N0}] " +
                    $"rows updated {rowsUpdated:N0}, lookups failed {lookupFailed:N0} " +
                    $"| ETA {eta:d\\d\\ hh\\:mm\\:ss}");
            }
        }


        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine("MusicBrainz Enrichment Complete");
        Console.WriteLine($"Recordings looked up: {processed:N0}");
        Console.WriteLine($"Rows updated:         {rowsUpdated:N0}");
        Console.WriteLine($"Lookups failed:       {lookupFailed:N0}");
        Console.WriteLine($"Total time:           {stopwatch.Elapsed:d\\d\\ hh\\:mm\\:ss}");
        Console.WriteLine("=================================");
    }
}
