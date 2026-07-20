using Microsoft.Data.Sqlite;

namespace TidyTunes.Data.Migrations;

public class DatabaseMigrator
{
    private readonly DatabaseService _database;

    public DatabaseMigrator(DatabaseService database)
    {
        _database = database;
    }


    public void ApplyMigrations()
    {
        using var connection = _database.GetConnection();

        connection.Open();

        CreateMigrationTable(connection);

        var version = GetVersion(connection);


        if (version < 1)
        {
            Console.WriteLine("Applying Migration 001...");

            Migration001.Apply(connection);

            SetVersion(connection, 1);

            version = 1;
        }


        if (version < 2)
        {
            Console.WriteLine("Applying Migration 002...");

            Migration002.Apply(connection);

            SetVersion(connection, 2);

            version = 2;
        }


        if (version < 3)
        {
            Console.WriteLine("Applying Migration 003...");

            Migration003.Apply(connection);

            SetVersion(connection, 3);

            version = 3;
        }


        if (version < 4)
        {
            Console.WriteLine("Applying Migration 004...");

            Migration004.Apply(connection);

            SetVersion(connection, 4);

            version = 4;
        }


        if (version < 5)
        {
            Console.WriteLine("Applying Migration 005...");

            Migration005.Apply(connection);

            SetVersion(connection, 5);

            version = 5;
        }


        if (version < 6)
        {
            Console.WriteLine("Applying Migration 006...");

            Migration006.Apply(connection);

            SetVersion(connection, 6);

            version = 6;
        }


        if (version < 7)
        {
            Console.WriteLine("Applying Migration 007...");

            Migration007.Apply(connection);

            SetVersion(connection, 7);

            version = 7;
        }


        if (version < 8)
        {
            Console.WriteLine("Applying Migration 008...");

            Migration008.Apply(connection);

            SetVersion(connection, 8);
        }
    }


    private void CreateMigrationTable(
        SqliteConnection connection)
    {
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS SchemaVersion
        (
            Version INTEGER NOT NULL
        );


        INSERT INTO SchemaVersion (Version)
        SELECT 0
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM SchemaVersion
        );
        """;

        command.ExecuteNonQuery();
    }


    private int GetVersion(
        SqliteConnection connection)
    {
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT Version
        FROM SchemaVersion
        LIMIT 1;
        """;

        return Convert.ToInt32(
            command.ExecuteScalar());
    }


    private void SetVersion(
        SqliteConnection connection,
        int version)
    {
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE SchemaVersion
        SET Version = $version;
        """;

        command.Parameters.AddWithValue(
            "$version",
            version);

        command.ExecuteNonQuery();
    }
}