using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TidyTunes.Core;

namespace TidyTunes.Scanner.Services;

public class ArtistRename
{
    public string OldName { get; set; } = string.Empty;

    public string NewName { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}


// Merges spelling variants of the same artist and fixes shouting:
//   - "Ángel y Khriz" / "Angel y Khriz" / "Ángel y Kris" fold to one
//     canonical spelling (the one with the most files)
//   - "Andrew Spencer & The Vamprockers" / "...And The Vamprockerz"
//     collapse via the same folded key
//   - "MEN AT WORK" becomes "Men at Work" (single-word names like
//     "DMX" or "ABBA" are left alone - they may be acronyms)
public class ArtistCanonicalizationService
{
    private static readonly Regex Word = new(
        @"[\p{L}\p{N}']+",
        RegexOptions.Compiled);


    public List<ArtistRename> Plan(
        IEnumerable<(string Artist, long FileCount)> knownArtists)
    {
        var renames = new List<ArtistRename>();

        // Group name variants by folded key; canonical = most files.

        var groups = new Dictionary<string, List<(string Name, long Count)>>();

        foreach (var (artist, count) in knownArtists)
        {
            var key = FoldKey(artist);

            if (key.Length == 0)
            {
                continue;
            }

            if (!groups.TryGetValue(key, out var variants))
            {
                variants = new List<(string, long)>();
                groups[key] = variants;
            }

            var existing = variants.FindIndex(v => v.Name == artist);

            if (existing >= 0)
            {
                variants[existing] = (artist, variants[existing].Count + count);
            }
            else
            {
                variants.Add((artist, count));
            }
        }


        foreach (var variants in groups.Values)
        {
            var canonical = variants
                .OrderByDescending(v => v.Count)
                // Prefer a variant that isn't shouting when counts tie.
                .ThenBy(v => TextCasing.IsAllCapsMultiWord(v.Name) ? 1 : 0)
                .First().Name;

            var target = TextCasing.IsAllCapsMultiWord(canonical)
                ? TextCasing.ToTitleCase(canonical)
                : canonical;

            foreach (var (name, _) in variants)
            {
                if (name != target)
                {
                    renames.Add(new ArtistRename
                    {
                        OldName = name,
                        NewName = target,
                        Reason = name == canonical ? "caps" : "variant"
                    });
                }
            }
        }

        return renames;
    }


    // Folding shared with the organizer via Core.
    public static string FoldKey(string name)
    {
        return ArtistNameFolding.FoldKey(name);
    }


}
