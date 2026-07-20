using Microsoft.Data.Sqlite;

namespace TidyTunes.Data;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();
    }

    public SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public bool TestConnection()
    {
        try
        {
            using var connection = GetConnection();
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
}