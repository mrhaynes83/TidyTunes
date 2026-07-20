using Microsoft.Data.Sqlite;

namespace TidyTunes.Data.Migrations;

public static class Migration004
{
    public static void Apply(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        ALTER TABLE LibraryFiles
        ADD COLUMN Md5Hash TEXT;

        ALTER TABLE LibraryFiles
        ADD COLUMN Sha256Hash TEXT;

        ALTER TABLE LibraryFiles
        ADD COLUMN FingerprintStatus TEXT DEFAULT 'Pending';

        ALTER TABLE LibraryFiles
        ADD COLUMN FingerprintGeneratedDate TEXT;

        ALTER TABLE LibraryFiles
        ADD COLUMN AcoustIdConfidence REAL DEFAULT 0;

        ALTER TABLE LibraryFiles
        ADD COLUMN MusicBrainzStatus TEXT DEFAULT 'Pending';

        ALTER TABLE LibraryFiles
        ADD COLUMN MetadataConfidence REAL DEFAULT 0;
        """;

        command.ExecuteNonQuery();
    }
}