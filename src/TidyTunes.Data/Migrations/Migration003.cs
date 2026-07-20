using Microsoft.Data.Sqlite;

namespace TidyTunes.Data.Migrations;

public static class Migration003
{
    public static void Apply(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();

        command.CommandText =
        """
        ALTER TABLE LibraryFiles ADD COLUMN AlbumArtist TEXT;
        ALTER TABLE LibraryFiles ADD COLUMN Composer TEXT;
        ALTER TABLE LibraryFiles ADD COLUMN Genre TEXT;
        ALTER TABLE LibraryFiles ADD COLUMN Year INTEGER;
        ALTER TABLE LibraryFiles ADD COLUMN TrackNumber INTEGER;
        ALTER TABLE LibraryFiles ADD COLUMN DiscNumber INTEGER;

        ALTER TABLE LibraryFiles ADD COLUMN MusicBrainzRecordingId TEXT;
        ALTER TABLE LibraryFiles ADD COLUMN MusicBrainzReleaseId TEXT;
        ALTER TABLE LibraryFiles ADD COLUMN MusicBrainzArtistId TEXT;

        ALTER TABLE LibraryFiles ADD COLUMN AcoustIdFingerprint TEXT;
        ALTER TABLE LibraryFiles ADD COLUMN AcoustIdId TEXT;

        ALTER TABLE LibraryFiles ADD COLUMN Codec TEXT;
        ALTER TABLE LibraryFiles ADD COLUMN Container TEXT;

        ALTER TABLE LibraryFiles ADD COLUMN ReplayGainTrack REAL;
        ALTER TABLE LibraryFiles ADD COLUMN ReplayGainAlbum REAL;

        ALTER TABLE LibraryFiles ADD COLUMN Bpm INTEGER;

        ALTER TABLE LibraryFiles ADD COLUMN HasArtwork INTEGER DEFAULT 0;
        ALTER TABLE LibraryFiles ADD COLUMN Comment TEXT;
        ALTER TABLE LibraryFiles ADD COLUMN HasLyrics INTEGER DEFAULT 0;

        ALTER TABLE LibraryFiles ADD COLUMN LastAnalyzed TEXT;
        ALTER TABLE LibraryFiles ADD COLUMN LastMetadataRefresh TEXT;
        """;

        command.ExecuteNonQuery();
    }
}