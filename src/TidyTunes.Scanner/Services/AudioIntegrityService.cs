using System.Diagnostics;
using TidyTunes.Data.Repositories;

namespace TidyTunes.Scanner.Services;

// Verifies files are actually playable by fully decoding them with
// ffmpeg ("-f null" discards the decoded audio), which catches what
// playback would: truncated files, corrupt frames, bad headers, and
// files whose extension lies about their contents. Runs several
// ffmpeg processes in parallel since a single decode is mostly
// CPU-bound, commits results in batches, and skips already-checked
// files so the job is resumable, per the project's batch rules.
public class AudioIntegrityService
{
    private readonly LibraryFileRepository _repository;
    private readonly string _ffmpegPath;

    private const int BatchSize = 500;

    private const int TimeoutMilliseconds = 180000;

    private const int MaxErrorLength = 500;


    public AudioIntegrityService(
        LibraryFileRepository repository,
        string ffmpegPath)
    {
        _repository = repository;
        _ffmpegPath = ffmpegPath;
    }


    // Confirms ffmpeg can actually be found before starting a long
    // batch job, mirroring FingerprintService.IsAvailable().
    public bool IsAvailable(out string resolvedPath)
    {
        resolvedPath = ResolvePath();

        return File.Exists(resolvedPath);
    }


    private string ResolvePath()
    {
        if (File.Exists(_ffmpegPath))
        {
            return _ffmpegPath;
        }

        var withExtension = _ffmpegPath + ".exe";

        if (File.Exists(withExtension))
        {
            return withExtension;
        }

        // Return the original path so callers see the path the user
        // configured when reporting "not found" errors.
        return _ffmpegPath;
    }


    public void Run()
    {
        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine("Audio Integrity Check");
        Console.WriteLine("=================================");
        Console.WriteLine();


        var files = _repository.GetFilesNeedingIntegrityCheck();

        if (files.Count == 0)
        {
            Console.WriteLine(
                "All files have already been checked.");

            Console.WriteLine();
            return;
        }


        var workerCount = Math.Clamp(
            Environment.ProcessorCount - 2, 2, 8);

        Console.WriteLine($"Files to check: {files.Count:N0}");
        Console.WriteLine($"Parallel decoders: {workerCount}");
        Console.WriteLine();


        var exePath = ResolvePath();

        var processed = 0;
        var passed = 0;
        var failed = 0;
        var notFound = 0;

        var pendingUpdates = new List<(long Id, string Status, string? Error)>(BatchSize);

        var pendingLock = new object();

        var stopwatch = Stopwatch.StartNew();

        var lastReportTime = TimeSpan.Zero;


        Parallel.ForEach(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = workerCount },
            file =>
            {
                var (status, error) = CheckFile(exePath, file.FilePath);

                switch (status)
                {
                    case "Passed":
                        Interlocked.Increment(ref passed);
                        break;

                    case "Failed":
                        Interlocked.Increment(ref failed);
                        break;

                    default:
                        Interlocked.Increment(ref notFound);
                        break;
                }

                var processedSoFar =
                    Interlocked.Increment(ref processed);


                lock (pendingLock)
                {
                    pendingUpdates.Add((file.Id, status, error));

                    if (pendingUpdates.Count >= BatchSize)
                    {
                        _repository.UpdateIntegrityResultsBatch(pendingUpdates);
                        pendingUpdates.Clear();
                    }


                    // Same throttled progress reporting as the hash
                    // maintenance job: every 250 files or 2 seconds.

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
                            $"{rate:N1} files/sec, ETA {eta:hh\\:mm\\:ss}, " +
                            $"failed: {failed:N0}");
                    }
                }
            });


        lock (pendingLock)
        {
            if (pendingUpdates.Count > 0)
            {
                _repository.UpdateIntegrityResultsBatch(pendingUpdates);
                pendingUpdates.Clear();
            }
        }


        var issueCount = _repository.SyncCorruptAudioIssues();


        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine("Audio Integrity Check Complete");
        Console.WriteLine($"Checked:       {processed:N0}");
        Console.WriteLine($"Passed:        {passed:N0}");
        Console.WriteLine($"Failed:        {failed:N0}");
        Console.WriteLine($"File missing:  {notFound:N0}");
        Console.WriteLine($"Total time:    {stopwatch.Elapsed:hh\\:mm\\:ss}");
        Console.WriteLine();
        Console.WriteLine(
            $"Corrupt files recorded as Issues: {issueCount:N0}");
        Console.WriteLine("=================================");
    }


    private static (string Status, string? Error) CheckFile(
        string exePath,
        string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return ("FileNotFound", null);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // ArgumentList handles quoting, so filenames containing
            // quotes or other special characters can't break the
            // command line.
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-nostdin");
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("null");
            startInfo.ArgumentList.Add("-");

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return ("Failed", "Could not start ffmpeg");
            }

            // Drain both streams asynchronously so a full pipe buffer
            // can never deadlock against WaitForExit.
            var errorTask = process.StandardError.ReadToEndAsync();
            var outputTask = process.StandardOutput.ReadToEndAsync();

            if (!process.WaitForExit(TimeoutMilliseconds))
            {
                process.Kill(entireProcessTree: true);

                return ("Failed", "Decode timed out");
            }

            var error = errorTask.GetAwaiter().GetResult().Trim();

            outputTask.GetAwaiter().GetResult();

            // ffmpeg can exit 0 while still reporting decode errors on
            // stderr (it keeps going past damaged frames), so both the
            // exit code and stderr count as failures.
            if (process.ExitCode == 0 && error.Length == 0)
            {
                return ("Passed", null);
            }

            if (error.Length == 0)
            {
                error = $"ffmpeg exited with code {process.ExitCode}";
            }
            else if (error.Length > MaxErrorLength)
            {
                error = error[..MaxErrorLength];
            }

            return ("Failed", error);
        }
        catch (Exception ex)
        {
            return ("Failed", $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
