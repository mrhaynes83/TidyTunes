using Microsoft.Data.Sqlite;

namespace TidyTunes.Data.Migrations;

public static class Migration007
{
    // DJ-oriented metadata and issue lifecycle:
    //  - MusicalKey stores the embedded initial key (TKEY) written by
    //    DJ tools like Serato / Traktor / Mixed In Key
    //  - Issues gains a Status lifecycle (Open / Ignored / Resolved /
    //    FalsePositive) per the project spec
    public static void Apply(SqliteConnection connection)
    {
        AddColumnIfMissing(
            connection,
            "LibraryFiles",
            "MusicalKey",
            "TEXT");

        AddColumnIfMissing(
            connection,
            "Issues",
            "Status",
            "TEXT DEFAULT 'Open'");

        AddColumnIfMissing(
            connection,
            "Issues",
            "ResolvedDate",
            "TEXT");
    }


    private static void AddColumnIfMissing(
        SqliteConnection connection,
        string table,
        string column,
        string definition)
    {
        if (ColumnExists(connection, table, column))
        {
            return;
        }

        using var command = connection.CreateCommand();

        command.CommandText =
            $"ALTER TABLE {table} ADD COLUMN {column} {definition};";

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
