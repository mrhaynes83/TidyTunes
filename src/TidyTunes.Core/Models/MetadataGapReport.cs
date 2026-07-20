namespace TidyTunes.Core.Models;

public class MetadataGapReport
{
    public long TotalFilesWithGaps { get; set; }

    public long MissingArtistCount { get; set; }

    public long MissingAlbumCount { get; set; }

    public long MissingTitleCount { get; set; }

    // Files missing Artist AND Album AND Title - the worst offenders,
    // likely to need manual attention or fingerprint-based identification.
    public long MissingAllCount { get; set; }

    public List<LibraryFile> WorstOffenders { get; set; } = new();
}
