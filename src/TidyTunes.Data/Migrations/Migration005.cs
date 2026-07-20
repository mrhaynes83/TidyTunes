using Microsoft.Data.Sqlite;

namespace TidyTunes.Data.Migrations;

public static class Migration005
{
    // DuplicateGroups was originally created by SchemaManager.EnsureSchema()
    // (which runs before this migrator, in Program.cs) with a different,
    // older shape than Migration002 defines. Because Migration002 uses
    // CREATE TABLE IF NOT EXISTS, it silently skipped adding FileCount
    // once the table already existed. This migration adds the missing
    // column without touching any existing rows or data.
    public static void Apply(SqliteConnection connection)
    {
        Console.WriteLine("Applying Migration 005...");

        if (!ColumnExists(connection, "DuplicateGroups", "FileCount"))
        {
            using var addColumn = connection.CreateCommand();

            addColumn.CommandText =
            """
            ALTER TABLE DuplicateGroups
            ADD COLUMN FileCount INTEGER;
            """;

            addColumn.ExecuteNonQuery();
        }


        // Backfill FileCount for any pre-existing groups by counting
        // their actual matches, so nothing is left NULL.

        using var backfill = connection.CreateCommand();

        backfill.CommandText =
        """
        UPDATE DuplicateGroups
        SET FileCount =
        (
            SELECT COUNT(*)
            FROM DuplicateMatches
            WHERE DuplicateMatches.DuplicateGroupId = DuplicateGroups.Id
        )
        WHERE FileCount IS NULL;
        """;

        backfill.ExecuteNonQuery();
    }


    private static bool ColumnExists(
        SqliteConnection connection,
        string table,
        string column)
    {
        using var command = connection.CreateCommand();

        command.CommandText = $"PRAGMA table_info({table});";

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            // Column "name" is at ordinal 1 in PRAGMA table_info results.
            var name = reader.GetString(1);

            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
