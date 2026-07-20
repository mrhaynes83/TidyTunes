using Microsoft.Data.Sqlite;

namespace TidyTunes.Data.Migrations;

public static class Migration008
{
    // Quarantine tracking for duplicate resolution: when a duplicate
    // copy is moved out of the library, its row keeps playing along -
    // FilePath points at the quarantine location, OriginalPath
    // remembers where it came from, and QuarantinedDate marks when.
    // Restoring a file is therefore just a move back plus clearing
    // these two fields.
    public static void Apply(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "OriginalPath", "TEXT");

        AddColumnIfMissing(connection, "QuarantinedDate", "TEXT");
    }


    private static void AddColumnIfMissing(
        SqliteConnection connection,
        string column,
        string definition)
    {
        if (ColumnExists(connection, "LibraryFiles", column))
        {
            return;
        }

        using var command = connection.CreateCommand();

        command.CommandText =
            $"ALTER TABLE LibraryFiles ADD COLUMN {column} {definition};";

        command.ExecuteNonQuery();
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
