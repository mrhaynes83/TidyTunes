using TagLib;
using TidyTunes.Core.Models;

namespace TidyTunes.Scanner.Services;

public class MetadataExtractor
{
    public LibraryFile Extract(string filePath)
    {
        var info = new FileInfo(filePath);

        var libraryFile = new LibraryFile
        {
            FilePath = filePath,
            FileName = info.Name,
            Extension = info.Extension.ToLowerInvariant(),
            FileSize = info.Length,
            ModifiedDate = info.LastWriteTime,
            DateAdded = DateTime.UtcNow
        };


        try
        {
            using var tagFile = TagLib.File.Create(filePath);


            // Basic Metadata

            libraryFile.Artist =
                tagFile.Tag.FirstPerformer;

            libraryFile.Album =
                tagFile.Tag.Album;

            libraryFile.Title =
                tagFile.Tag.Title;

            libraryFile.AlbumArtist =
                tagFile.Tag.AlbumArtists.Length > 0
                    ? tagFile.Tag.AlbumArtists[0]
                    : null;

            libraryFile.Genre =
                tagFile.Tag.Genres.Length > 0
                    ? tagFile.Tag.Genres[0]
                    : null;

            libraryFile.Comment =
                tagFile.Tag.Comment;


            // Track Information

            libraryFile.TrackNumber =
                tagFile.Tag.Track > 0
                    ? (int)tagFile.Tag.Track
                    : null;

            libraryFile.DiscNumber =
                tagFile.Tag.Disc > 0
                    ? (int)tagFile.Tag.Disc
                    : null;


            // Year

            libraryFile.Year =
                tagFile.Tag.Year > 0
                    ? (int)tagFile.Tag.Year
                    : null;


            // DJ Metadata

            libraryFile.Bpm =
                tagFile.Tag.BeatsPerMinute > 0
                    ? (int)tagFile.Tag.BeatsPerMinute
                    : null;

            libraryFile.MusicalKey =
                string.IsNullOrWhiteSpace(tagFile.Tag.InitialKey)
                    ? null
                    : tagFile.Tag.InitialKey;

            libraryFile.Composer =
                tagFile.Tag.FirstComposer;


            // MusicBrainz IDs already embedded in tags (Picard etc.)

            libraryFile.MusicBrainzRecordingId =
                tagFile.Tag.MusicBrainzTrackId;

            libraryFile.MusicBrainzReleaseId =
                tagFile.Tag.MusicBrainzReleaseId;

            libraryFile.MusicBrainzArtistId =
                tagFile.Tag.MusicBrainzArtistId;


            // Technical Information

            libraryFile.DurationSeconds =
                (int)Math.Round(
                    tagFile.Properties.Duration.TotalSeconds);

            libraryFile.BitRate =
                tagFile.Properties.AudioBitrate;

            libraryFile.SampleRate =
                tagFile.Properties.AudioSampleRate;

            libraryFile.Channels =
                tagFile.Properties.AudioChannels;


            // Container / Codec

            libraryFile.Container =
                tagFile.MimeType;

            libraryFile.Codec =
                tagFile.Properties.Description;


            // Artwork

            libraryFile.HasArtwork =
                tagFile.Tag.Pictures.Length > 0;


            // Lyrics detection

            libraryFile.HasLyrics =
                !string.IsNullOrWhiteSpace(
                    tagFile.Tag.Lyrics);
        }
        catch
        {
            // Metadata failures will be tracked later
            // through the Issues system.
        }


        libraryFile.LastMetadataRefresh =
            DateTime.UtcNow;


        return libraryFile;
    }
}