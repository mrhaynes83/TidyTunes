namespace TidyTunes.Core.Models;

public class LibraryStatistics
{
    public long TotalFiles { get; set; }

    public long ArtistCount { get; set; }

    public long AlbumCount { get; set; }


    // File Types: Extension -> Count

    public Dictionary<string, long> FileTypeCounts { get; set; } = new();


    // Metadata Health

    public long MissingArtist { get; set; }

    public long MissingAlbum { get; set; }

    public long MissingTitle { get; set; }


    // Hash Status

    public long Md5Complete { get; set; }

    public long Sha256Complete { get; set; }


    // Database Health

    public long DuplicateGroups { get; set; }

    public long Issues { get; set; }
}
