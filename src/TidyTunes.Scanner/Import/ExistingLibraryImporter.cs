using Microsoft.Data.Sqlite;
using TidyTunes.Core.Models;
using TidyTunes.Data.Repositories;

namespace TidyTunes.Scanner.Import;

public class ExistingLibraryImporter
{
    private readonly string _sourceDatabase;
    private readonly LibraryFileRepository _repository;

    public ExistingLibraryImporter(
        string sourceDatabase,
        LibraryFileRepository repository)
    {
        _sourceDatabase = sourceDatabase;
        _repository = repository;
    }

    public int Import()
    {
        var imported = 0;

        using var connection = new SqliteConnection(
            $"Data Source={_sourceDatabase}");

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
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
        FROM MusicFiles;
        """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var file = new LibraryFile
            {
                FilePath = reader["FilePath"].ToString() ?? string.Empty,
                FileName = reader["FileName"].ToString(),
                Extension = reader["Extension"].ToString(),
                FileSize = Convert.ToInt64(reader["FileSize"]),
                Artist = reader["Artist"].ToString(),
                Album = reader["Album"].ToString(),
                Title = reader["Title"].ToString(),
                FileHash = reader["FileHash"].ToString(),
                AudioHash = reader["AudioHash"].ToString(),
                DateAdded = DateTime.UtcNow
            };

            _repository.Add(file);

            imported++;
        }

        return imported;
    }
}