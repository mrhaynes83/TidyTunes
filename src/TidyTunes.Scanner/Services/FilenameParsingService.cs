using System.Text.RegularExpressions;

namespace TidyTunes.Scanner.Services;

public class ParsedFilename
{
    public string? Artist { get; set; }

    public string? Title { get; set; }

    public int? TrackNumber { get; set; }
}


// Extracts Artist/Title (and a leading track number) from filenames
// like "Artist - Title.mp3", "01 - Title.mp3", "Artist ~ Title.mp3"
// or "Artist_Title.mp3", stripping YouTube-style junk suffixes
// ("(Official Video)", "[HD]", "with lyrics", ...) along the way.
// Meaningful qualifiers (Remix, Live, Acoustic, feat. X) are kept -
// they distinguish versions a DJ cares about.
public class FilenameParsingService
{
    // Junk groups: "(Official Video)", "[Lyrics]", "(HD)", etc.
    // Only groups made of known junk words are removed; "(Club Remix)"
    // stays because "remix" is not in this list.
    private static readonly Regex JunkGroup = new(
        @"\s*[\(\[\{](?=[^\)\]\}]*(official|video|audio|lyric|lyrics|hd|hq|4k|1080p|720p|visuali[sz]er|full\s+song|new\s+song|out\s+now|free\s+download|download|premiere|exclusive|copyright|napster|youtube|mp3|m/?v))[^\)\]\}]*[\)\]\}]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Bare junk phrases without brackets at the end of the name, e.g.
    // "Song with lyrics", "Song official video", "Song ~ with lyrics".
    private static readonly Regex JunkTail = new(
        @"[\s~\-_]+(with\s+lyrics|w/\s*lyrics|lyrics?|official\s+(music\s+)?video|official\s+audio|music\s+video|official)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Leading track number: "01 - ", "01. ", "01_", "1-" etc.
    private static readonly Regex LeadingTrackNumber = new(
        @"^(\d{1,3})[\s]*[.\-_][\s]*",
        RegexOptions.Compiled);

    private static readonly string[] Separators =
        { " - ", " – ", " — ", " ~ ", " _ " };


    public ParsedFilename Parse(string fileName)
    {
        var result = new ParsedFilename();

        var name = Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrWhiteSpace(name))
        {
            return result;
        }


        // Underscore-style names ("Artist_Title") only get their
        // underscores converted when the name has no spaces at all -
        // otherwise underscores may be intentional.

        if (!name.Contains(' ') && name.Contains('_'))
        {
            name = name.Replace('_', ' ');
        }


        // Strip junk, tolerating several layers ("Song (Official
        // Video) [HD] with lyrics").

        string previous;

        do
        {
            previous = name;

            name = JunkGroup.Replace(name, "");
            name = JunkTail.Replace(name, "");
        }
        while (name != previous);

        name = Normalize(name);

        if (name.Length == 0)
        {
            return result;
        }


        // Leading track number.

        var trackMatch = LeadingTrackNumber.Match(name);

        if (trackMatch.Success)
        {
            var candidate = Normalize(
                name[trackMatch.Length..]);

            // Only treat it as a track number when something usable
            // follows ("03 - Song"), not for names that ARE numbers.
            if (candidate.Length >= 2)
            {
                result.TrackNumber = int.Parse(trackMatch.Groups[1].Value);
                name = candidate;
            }
        }


        // Split on the first separator found. Additional separators
        // stay inside the title ("Artist - Song - Live" ->
        // title "Song - Live"), which errs on the side of keeping
        // information rather than guessing an album.

        foreach (var separator in Separators)
        {
            var index = name.IndexOf(separator, StringComparison.Ordinal);

            if (index <= 0)
            {
                continue;
            }

            var left = Normalize(name[..index]);
            var right = Normalize(name[(index + separator.Length)..]);

            if (left.Length >= 2 && right.Length >= 2)
            {
                result.Artist = left;
                result.Title = right;

                return result;
            }
        }


        // No separator: the name alone is at best a title.

        if (name.Length >= 2)
        {
            result.Title = name;
        }

        return result;
    }


    private static string Normalize(string value)
    {
        value = Regex.Replace(value, @"\s+", " ").Trim();

        // Trim leftover separator punctuation after junk removal,
        // e.g. "Song - " or "- Song".
        return value.Trim(' ', '-', '~', '_', '.');
    }
}
