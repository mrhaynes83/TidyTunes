using Microsoft.Data.Sqlite;

namespace TidyTunes.Data.Migrations;

public static class Migration001
{
    public static void Apply(SqliteConnection connection)
    {
        Console.WriteLine("Creating base database schema...");

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS LibraryFiles
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            FilePath TEXT NOT NULL UNIQUE,
            FileName TEXT,
            Extension TEXT,
            FileSize INTEGER,
            ModifiedDate TEXT,

            Artist TEXT,
            Album TEXT,
            Title TEXT,
            Genre TEXT,

            FileHash TEXT,
            AudioHash TEXT,
            AcoustId TEXT,

            DurationSeconds INTEGER,
            BitRate INTEGER,
            SampleRate INTEGER,
            Channels INTEGER,

            DateAdded TEXT NOT NULL
        );


        CREATE TABLE IF NOT EXISTS ScanHistory
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            StartTime TEXT NOT NULL,
            EndTime TEXT,
            FilesScanned INTEGER,
            Status TEXT
        );


        CREATE TABLE IF NOT EXISTS Issues
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            LibraryFileId INTEGER,
            IssueType TEXT NOT NULL,
            Description TEXT,
            DetectedDate TEXT NOT NULL,

            FOREIGN KEY(LibraryFileId)
            REFERENCES LibraryFiles(Id)
        );
        """;

        command.ExecuteNonQuery();
    }
}