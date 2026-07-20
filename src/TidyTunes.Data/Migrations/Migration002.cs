using Microsoft.Data.Sqlite;

namespace TidyTunes.Data.Migrations;

public static class Migration002
{
    public static void Apply(SqliteConnection connection)
    {
        Console.WriteLine("Applying Migration 002...");

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS DuplicateGroups
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            CreatedDate TEXT NOT NULL,
            DuplicateType TEXT NOT NULL,
            FileCount INTEGER NOT NULL
        );


        CREATE TABLE IF NOT EXISTS DuplicateMatches
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            DuplicateGroupId INTEGER NOT NULL,
            LibraryFileId INTEGER NOT NULL,
            MatchMethod TEXT NOT NULL,
            Confidence REAL,

            FOREIGN KEY(DuplicateGroupId)
                REFERENCES DuplicateGroups(Id),

            FOREIGN KEY(LibraryFileId)
                REFERENCES LibraryFiles(Id)
        );


        CREATE INDEX IF NOT EXISTS IX_DuplicateMatches_GroupId
        ON DuplicateMatches(DuplicateGroupId);


        CREATE INDEX IF NOT EXISTS IX_DuplicateMatches_FileId
        ON DuplicateMatches(LibraryFileId);
        """;

        command.ExecuteNonQuery();
    }
}