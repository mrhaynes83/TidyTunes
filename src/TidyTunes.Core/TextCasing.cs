using System.Text;
using System.Text.RegularExpressions;

namespace TidyTunes.Core;

public static class TextCasing
{
    private static readonly Regex Word = new(
        @"[\p{L}\p{N}']+",
        RegexOptions.Compiled);

    // Lowercase connectors in Title Case ("Men at Work"), except at
    // the start of the name.
    private static readonly HashSet<string> SmallWords = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "at", "de", "el", "for", "in", "la",
        "of", "on", "the", "to", "vs", "y"
    };


    // All letters uppercase across more than one word - single words
    // like "DMX" may be acronyms and are not considered shouting.
    public static bool IsAllCapsMultiWord(string name)
    {
        var letters = name.Where(char.IsLetter).ToArray();

        return letters.Length >= 2
            && letters.All(char.IsUpper)
            && name.Trim().Contains(' ');
    }


    public static string ToTitleCase(string name)
    {
        var result = new StringBuilder(name.Length);

        var lastIndex = 0;

        var isFirstWord = true;

        foreach (Match match in Word.Matches(name))
        {
            result.Append(name, lastIndex, match.Index - lastIndex);

            var word = match.Value;

            if (word.Length <= 2 && !SmallWords.Contains(word))
            {
                // "DJ", "MC", "LL" keep their capitals.
                result.Append(word);
            }
            else if (!isFirstWord && SmallWords.Contains(word))
            {
                result.Append(word.ToLowerInvariant());
            }
            else
            {
                result.Append(char.ToUpperInvariant(word[0]));
                result.Append(word[1..].ToLowerInvariant());
            }

            lastIndex = match.Index + match.Length;
            isFirstWord = false;
        }

        result.Append(name, lastIndex, name.Length - lastIndex);

        return result.ToString();
    }
}
