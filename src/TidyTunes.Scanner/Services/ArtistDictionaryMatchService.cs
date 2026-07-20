using System.Text.RegularExpressions;
using TidyTunes.Core.Models;

namespace TidyTunes.Scanner.Services;

public class ArtistDictionaryMatch
{
    public long Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string Artist { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
}


// Last-resort artist recovery for files whose names contain the
// artist without any separator ("leo sayer when i need you.mp3",
// "rubber bullets 10cc.mp3"): the library's own artists become the
// dictionary, and a file matches when a known artist appears as a
// whole-word run at the START or END of its title. Longest match
// wins; anything ambiguous or unmatched is left exactly as it is.
public class ArtistDictionaryMatchService
{
    // token runs must line up on word boundaries so "Cher" can never
    // match inside "Cherish".
    private static readonly Regex Tokenizer = new(
        @"[\p{L}\p{N}']+",
        RegexOptions.Compiled);


    private readonly Dictionary<string, string> _artistsByKey = new();

    // Longest artists first, so "Future Islands" is tried before "Future".
    private readonly List<(string Key, string[] Tokens, string Canonical)> _artists = new();


    // knownArtists carries (name, file count): the count weeds out
    // junk one-off artist strings that themselves came from earlier
    // bad parses ("Never Ever", "Time") - a real artist has several
    // files in a library this size.
    public ArtistDictionaryMatchService(
        IEnumerable<(string Artist, long FileCount)> knownArtists)
    {
        foreach (var (artist, fileCount) in knownArtists)
        {
            var tokens = Tokenize(artist);

            if (tokens.Length == 0)
            {
                continue;
            }

            // Dirty dictionary entries like "03 Leo Sayer" (track
            // number baked into the artist) would split titles wrong.
            if (char.IsDigit(artist.TrimStart()[0]))
            {
                continue;
            }

            // Single words need to be long enough AND common enough in
            // the library to be trusted ("Time" or "Live" are titles
            // more often than artists).
            if (tokens.Length == 1 && (tokens[0].Length < 4 || fileCount < 3))
            {
                continue;
            }

            // Multi-word one-offs are usually junk from earlier parses.
            if (fileCount < 2)
            {
                continue;
            }

            var key = string.Join(" ", tokens);

            if (!_artistsByKey.ContainsKey(key))
            {
                _artistsByKey[key] = artist;
                _artists.Add((key, tokens, artist));
            }
        }

        _artists.Sort((a, b) => b.Tokens.Length != a.Tokens.Length
            ? b.Tokens.Length.CompareTo(a.Tokens.Length)
            : b.Key.Length.CompareTo(a.Key.Length));
    }


    public int DictionarySize => _artists.Count;


    // Leading track numbers ("01 ", "17b - ") carry no artist/title
    // information and confuse both matching and the final title.
    private static readonly Regex LeadingTrackNumber = new(
        @"^\d{1,3}[a-b]?[\s.\-_]+",
        RegexOptions.Compiled);


    // Attempts to split "artist title" or "title artist". Returns null
    // when no known artist lines up - the file stays as-is. All
    // artists are tried at prefix position before any suffix match is
    // accepted, so "All Saints - Never Ever" resolves to the leading
    // artist even when the title half also exists as a dictionary entry.
    public ArtistDictionaryMatch? Match(long id, string fileName, string title)
    {
        title = LeadingTrackNumber.Replace(title.Trim(), "");

        var matches = Tokenizer.Matches(title);

        if (matches.Count < 2)
        {
            return null;
        }

        var tokens = new string[matches.Count];

        for (var i = 0; i < matches.Count; i++)
        {
            tokens[i] = matches[i].Value.ToLowerInvariant();
        }


        foreach (var (_, artistTokens, canonical) in _artists)
        {
            if (artistTokens.Length >= tokens.Length)
            {
                continue;
            }

            // Prefix: "leo sayer when i need you"
            if (TokensMatchAt(tokens, artistTokens, 0))
            {
                var cut = matches[artistTokens.Length].Index;

                return BuildResult(id, fileName, canonical, title[cut..]);
            }
        }

        foreach (var (_, artistTokens, canonical) in _artists)
        {
            if (artistTokens.Length >= tokens.Length)
            {
                continue;
            }

            // Suffix: "rubber bullets 10cc"
            var suffixStart = tokens.Length - artistTokens.Length;

            if (TokensMatchAt(tokens, artistTokens, suffixStart))
            {
                var cut = matches[suffixStart].Index;

                // A name sitting after "feat."/"ft."/"(" is the GUEST
                // ("Hey There (feat. Future)"), never the main artist.
                if (FeaturedContext.IsMatch(title[..cut]))
                {
                    continue;
                }

                return BuildResult(id, fileName, canonical, title[..cut]);
            }
        }

        return null;
    }


    private static readonly Regex FeaturedContext = new(
        @"(\bfeat\.?|\bft\.?|\bfeaturing|\bwith|[(\[])\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);


    private static bool TokensMatchAt(
        string[] tokens,
        string[] artistTokens,
        int offset)
    {
        for (var i = 0; i < artistTokens.Length; i++)
        {
            if (tokens[offset + i] != artistTokens[i])
            {
                return false;
            }
        }

        return true;
    }


    private static ArtistDictionaryMatch? BuildResult(
        long id,
        string fileName,
        string artist,
        string remainder)
    {
        var title = remainder.Trim(' ', '-', '~', '_', '.', ',', '&');

        title = Regex.Replace(title, @"\s+", " ").Trim();

        if (title.Length < 2)
        {
            return null;
        }

        return new ArtistDictionaryMatch
        {
            Id = id,
            FileName = fileName,
            Artist = artist,
            Title = title
        };
    }


    private static string[] Tokenize(string value)
    {
        var matches = Tokenizer.Matches(value.ToLowerInvariant());

        var tokens = new string[matches.Count];

        for (var i = 0; i < matches.Count; i++)
        {
            tokens[i] = matches[i].Value;
        }

        return tokens;
    }
}
