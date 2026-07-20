namespace TidyTunes.Core.Models;

public class LibraryFile
{
    public long Id { get; set; }

    // File Information

    public string FilePath { get; set; } = string.Empty;

    public string? FileName { get; set; }

    public string? Extension { get; set; }

    public long FileSize { get; set; }

    public DateTime? ModifiedDate { get; set; }


    // Basic Music Metadata

    public string? Artist { get; set; }

    public string? Album { get; set; }

    public string? Title { get; set; }

    public string? AlbumArtist { get; set; }

    public string? Composer { get; set; }

    public string? Genre { get; set; }

    public int? Year { get; set; }

    public int? TrackNumber { get; set; }

    public int? DiscNumber { get; set; }


    // Identification

    public string? MusicBrainzRecordingId { get; set; }

    public string? MusicBrainzReleaseId { get; set; }

    public string? MusicBrainzArtistId { get; set; }

    public string? AcoustIdFingerprint { get; set; }

    public string? AcoustIdId { get; set; }


    // File Integrity

    public string? FileHash { get; set; }

    public string? AudioHash { get; set; }

public string? Md5Hash { get; set; }

public string? Sha256Hash { get; set; }

public string FingerprintStatus { get; set; } = "Pending";

public DateTime? FingerprintGeneratedDate { get; set; }

public double AcoustIdConfidence { get; set; }

public string MusicBrainzStatus { get; set; } = "Pending";

public double MetadataConfidence { get; set; }


    // Technical Audio Information

    public int? DurationSeconds { get; set; }

    public int? BitRate { get; set; }

    public int? SampleRate { get; set; }

    public int? Channels { get; set; }

    public string? Codec { get; set; }

    public string? Container { get; set; }


    // Audio Quality

    public double? ReplayGainTrack { get; set; }

    public double? ReplayGainAlbum { get; set; }

    public int? Bpm { get; set; }

    // Initial key (e.g. "8A", "Am") as written by DJ tools.
    public string? MusicalKey { get; set; }


    // Artwork / Extra Data

    public bool HasArtwork { get; set; }

    public string? Comment { get; set; }

    public bool HasLyrics { get; set; }


    // Integrity (decode test) outcome, when known.

    public string? IntegrityStatus { get; set; }


    // Tracking

    public DateTime DateAdded { get; set; }

    public DateTime? LastAnalyzed { get; set; }

    public DateTime? LastMetadataRefresh { get; set; }
}