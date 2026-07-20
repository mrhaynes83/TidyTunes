using Microsoft.Data.Sqlite;
using TidyTunes.Core.Models;

namespace TidyTunes.Scanner.Migration;

public class ExistingInventoryMigrator
{
    private readonly string _databasePath;

    public ExistingInventoryMigrator(string databasePath)
    {
        _databasePath = databasePath;
    }

    public int Migrate()
    {
        var migrated = 0;

        using var connection = new SqliteConnection(
            $"Data Source={_databasePath}");

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var readCommand = connection.CreateCommand();

        readCommand.CommandText =
        """
        SELECT
            FilePath,
            FileName,
            Extension,
            FileSize,
            ModifiedDate,
            Artist,
            Album,
            Title,
            FileHash,
            AudioHash,
            Duration,
            BitRate,
            SampleRate,
            Channels
        FROM Files;
        """;

        using var reader = readCommand.ExecuteReader();

        while (reader.Read())
        {
            using var insertCommand = connection.CreateCommand();

            insertCommand.CommandText =
            """
            INSERT OR IGNORE INTO LibraryFiles
            (
                FilePath,
                FileName,
                Extension,
                FileSize,
                ModifiedDate,
                Artist,
                Album,
                Title,
                FileHash,
                AudioHash,
                DurationSeconds,
                BitRate,
                SampleRate,
                Channels,
                DateAdded
            )
            VALUES
            (
                $path,
                $name,
                $extension,
                $size,
                $modified,
                $artist,
                $album,
                $title,
                $fileHash,
                $audioHash,
                $duration,
                $bitrate,
                $sampleRate,
                $channels,
                $dateAdded
            );
            """;

            insertCommand.Parameters.AddWithValue(
                "$path",
                reader["FilePath"]);

            insertCommand.Parameters.AddWithValue(
                "$name",
                reader["FileName"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$extension",
                reader["Extension"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$size",
                reader["FileSize"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$modified",
                reader["ModifiedDate"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$artist",
                reader["Artist"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$album",
                reader["Album"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$title",
                reader["Title"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$fileHash",
                reader["FileHash"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$audioHash",
                reader["AudioHash"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$duration",
                reader["Duration"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$bitrate",
                reader["BitRate"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$sampleRate",
                reader["SampleRate"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$channels",
                reader["Channels"] ?? DBNull.Value);

            insertCommand.Parameters.AddWithValue(
                "$dateAdded",
                DateTime.UtcNow);

            insertCommand.ExecuteNonQuery();

            migrated++;

            if (migrated % 1000 == 0)
            {
                Console.WriteLine(
                    $"Migrated {migrated:N0} files...");
            }
        }

        transaction.Commit();

        return migrated;
    }
}