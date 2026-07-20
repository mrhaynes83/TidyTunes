using System.Text.RegularExpressions;

namespace TidyTunes.Scanner.Services;

public class ArtistCleanupResult
{
    public long Id { get; set; }

    // Null artist means "could not be salvaged - blank it".
    public string? Artist { get; set; }

    public string? AlbumArtist { get; set; }

    // Replacement title when the old one was junk too; null = keep.
    public string? Title { get; set; }

    public string Reason { get; set; } = string.Empty;
}


// Repairs dirty embedded Artist tags: strips decorative wrappers
// ("[Steve Miller Band]", '"Weird Al" Yankovic'), removes leading
// track numbers, and blanks out junk that is not an artist at all -
// URL spam ("www.mp3-start.nl"), unknown-markers ("[no artist]"),
// bare numbers, and whole-track-strings ("[02]_dmx_-_it's_on").
// Blanked rows get their artist/title re-derived from the filename
// where possible, so most junk heals in the same pass.
public class ArtistTagCleanupService
{
    private static readonly Regex UrlJunk = new(
        @"(www\.|https?://|\.(com|net|org|biz|info|nl|ru|de|cz|pl)\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UnknownMarker = new(
        @"^(unknown( artist)?|no artist|various( artists)?|va|n/?a|soundtrack|artist)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LeadingTrackNumber = new(
        @"^\d{1,3}[a-b]?[\s.\-_]+",
        RegexOptions.Compiled);

    private static readonly char[] DecorationChars =
        { '_', '[', ']', '"', '~', '¦', '=', '«', '»', '@', '*', '.', '-', ' ', '(', ')', '{', '}', ':', ';', '|', '!' };


    private readonly FilenameParsingService _parser = new();

    private readonly HashSet<string> _trustedArtists;


    // trustedArtists: names with several files - anything matching one
    // (case-insensitively) is left untouched even when it looks odd
    // ("¥$", "_NSYNC", "1 Giant Leap" are real artists).
    public ArtistTagCleanupService(
        IEnumerable<(string Artist, long FileCount)> knownArtists)
    {
        _trustedArtists = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (artist, fileCount) in knownArtists)
        {
            if (fileCount >= 3)
            {
                _trustedArtists.Add(artist.Trim().ToLowerInvariant());
            }
        }
    }


    // Returns null when the row needs no change.
    public ArtistCleanupResult? Clean(
        long id,
        string fileName,
        string artist,
        string albumArtist)
    {
        var artistOutcome = CleanName(artist);
        var albumArtistOutcome = CleanName(albumArtist);

        if (artistOutcome.Unchanged && albumArtistOutcome.Unchanged)
        {
            return null;
        }

        var result = new ArtistCleanupResult
        {
            Id = id,
            Artist = artistOutcome.Cleaned,
            AlbumArtist = albumArtistOutcome.Cleaned,
            Reason = artistOutcome.Reason ?? albumArtistOutcome.Reason ?? ""
        };

        // When the artist tag was junked completely, the title very
        // likely carries the same dirt - re-derive both from the
        // filename, which at least describes the actual track.

        if (artistOutcome.Cleaned == null && artist.Length > 0)
        {
            var parsed = _parser.Parse(fileName);

            if (parsed.Artist != null)
            {
                result.Artist = parsed.Artist;
            }

            if (parsed.Title != null)
            {
                result.Title = parsed.Title;
            }
        }

        return result;
    }


    private (bool Unchanged, string? Cleaned, string? Reason) CleanName(
        string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (true, null, null);
        }

        // Real artists with several files keep their exact name, no
        // matter how strange it looks ("¥$", "1 Giant Leap") - but a
        // name STARTING with decoration ("(Max B. Vs. Akon)") is junk
        // regardless of how many files share it; bad rips come in
        // whole albums.
        if (_trustedArtists.Contains(name.Trim().ToLowerInvariant())
            && !UrlJunk.IsMatch(name)
            && !DecorationChars.Contains(name.TrimStart()[0]))
        {
            return (true, name, null);
        }

        // Junk that no cleanup can save.

        if (UrlJunk.IsMatch(name))
        {
            return (false, null, "url");
        }

        var stripped = name.Trim(DecorationChars);

        stripped = LeadingTrackNumber.Replace(stripped, "");

        // Inner decoration: '_Weird Al_ Yankovic' and '"Weird Al"
        // Yankovic' should both settle on 'Weird Al Yankovic'.
        stripped = stripped
            .Replace('_', ' ')
            .Replace("\"", "")
            .Replace("[", "")
            .Replace("]", "");

        stripped = Regex.Replace(stripped, @"\s+", " ").Trim();

        if (stripped.Length < 2
            || stripped.All(c => !char.IsLetter(c))
            || UnknownMarker.IsMatch(stripped))
        {
            return (false, null, "not-an-artist");
        }

        // Whole track strings ("dmx & the lox - get at me dog") make
        // no sense as an artist - blank and re-derive from filename.
        if (stripped.Contains(" - ") || stripped.Contains("_-_")
            || name.Length > 60)
        {
            return (false, null, "track-string");
        }

        if (stripped == name)
        {
            return (true, name, null);
        }

        return (false, stripped, "decorated");
    }
}
