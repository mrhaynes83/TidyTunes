using Microsoft.Data.Sqlite;
using TagLib;

namespace TidyTunes.Analysis;

public class AudioPropertyAnalyzer
{
    private readonly string _databasePath;

    public AudioPropertyAnalyzer(string databasePath)
    {
        _databasePath = databasePath;
    }

    public int Analyze()
    {
        using var connection =
            new SqliteConnection($"Data Source={_databasePath}");

        connection.Open();

        var select = connection.CreateCommand();

        select.CommandText = """
            SELECT
                Id,
                FilePath
            FROM LibraryFiles
            ORDER BY Id;
            """;

        using var reader = select.ExecuteReader();

        var files = new List<(long Id, string Path)>();

        while (reader.Read())
        {
            files.Add((
                reader.GetInt64(0),
                reader.GetString(1)));
        }

        reader.Close();

        Console.WriteLine(
            $"Analyzing {files.Count:N0} files...");
        Console.WriteLine();

        var processed = 0;

        foreach (var file in files)
        {
            processed++;

            if (!System.IO.File.Exists(file.Path))
                continue;

            try
            {
                using var audio =
                    TagLib.File.Create(file.Path);

                var update = connection.CreateCommand();

                update.CommandText = """
                    UPDATE LibraryFiles
                    SET
                        DurationSeconds = $duration,
                        BitRate = $bitrate,
                        SampleRate = $sampleRate,
                        Channels = $channels
                    WHERE Id = $id;
                    """;

                update.Parameters.AddWithValue(
                    "$duration",
                    (int)Math.Round(
                        audio.Properties.Duration.TotalSeconds));

                update.Parameters.AddWithValue(
                    "$bitrate",
                    audio.Properties.AudioBitrate);

                update.Parameters.AddWithValue(
                    "$sampleRate",
                    audio.Properties.AudioSampleRate);

                update.Parameters.AddWithValue(
                    "$channels",
                    audio.Properties.AudioChannels);

                update.Parameters.AddWithValue(
                    "$id",
                    file.Id);

                update.ExecuteNonQuery();
            }
            catch
            {
                // Ignore unreadable files for now.
            }

            if (processed % 1000 == 0)
            {
                Console.WriteLine(
                    $"{processed:N0} / {files.Count:N0}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Audio analysis complete.");

        return processed;
    }
}