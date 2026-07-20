using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TidyTunes.Core;

public static class ArtistNameFolding
{
    private static readonly Regex Word = new(
        @"[\p{L}\p{N}']+",
        RegexOptions.Compiled);


    // Folding for artist variant detection: lowercase, strip
    // diacritics, DROP connector words ("X & Y" == "X And Y", and
    // "The Beatles" == "Beatles" per the spec), drop 'h'
    // (Khriz/Kris), fold trailing z to s (Vamprockerz/Vamprockers),
    // strip punctuation, and SORT tokens so reordered credits like
    // "kriz y angel" vs "Ángel y Khriz" still collide.
    public static string FoldKey(string name)
    {
        var decomposed = name.ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c)
                != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        var words = Word.Matches(builder.ToString())
            .Select(m => m.Value)
            .Where(w => w is not ("and" or "y" or "et" or "und" or "the"))
            .Select(w => w.Replace("h", ""))
            .Select(w => w.EndsWith('z') ? w[..^1] + "s" : w)
            .Where(w => w.Length > 0)
            .OrderBy(w => w, StringComparer.Ordinal);

        return string.Join(" ", words);
    }
}
