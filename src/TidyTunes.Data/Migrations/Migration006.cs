using Microsoft.Data.Sqlite;

namespace TidyTunes.Data.Migrations;

public static class Migration006
{
    // Adds audio integrity (decode test) tracking to LibraryFiles.
    // The integrity check fully decodes each file with ffmpeg - the
    // automated equivalent of playing it - and records the outcome
    // here so the job is resumable and results are queryable.
    public static void Apply(SqliteConnection connection)
    {
        AddColumnIfMissing(
            connection,
            "IntegrityStatus",
            "TEXT DEFAULT 'Pending'");

        AddColumnIfMissing(
            connection,
            "IntegrityCheckedDate",
            "TEXT");

        AddColumnIfMissing(
            connection,
            "IntegrityError",
            "TEXT");
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
