using Microsoft.Data.Sqlite;

namespace TidyTunes.Data;

public class DatabaseInspector
{
    private readonly DatabaseService _database;

    public DatabaseInspector(DatabaseService database)
    {
        _database = database;
    }


    public void ShowStatus()
    {
        using var connection = _database.GetConnection();

        connection.Open();

        Console.WriteLine();

        ShowSchemaVersion(connection);

        ShowTableCount(connection, "LibraryFiles");
        ShowTableCount(connection, "DuplicateGroups");
        ShowTableCount(connection, "DuplicateMatches");
        ShowTableCount(connection, "Issues");
        ShowTableCount(connection, "ScanHistory");
    }


    private void ShowSchemaVersion(
        SqliteConnection connection)
    {
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT Version
        FROM SchemaVersion
        LIMIT 1;
        """;

        var version = command.ExecuteScalar();

        Console.WriteLine(
            $"Database Schema Version: {version}");
    }


    private void ShowTableCount(
        SqliteConnection connection,
        string table)
    {
        using var command = connection.CreateCommand();

        command.CommandText =
        $"""
        SELECT COUNT(*)
        FROM {table};
        """;

        var count = command.ExecuteScalar();

        Console.WriteLine(
            $"{table}: {count}");
    }
}