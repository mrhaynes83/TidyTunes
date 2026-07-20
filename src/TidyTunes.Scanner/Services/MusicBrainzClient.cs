using System.Text.Json;

namespace TidyTunes.Scanner.Services;

// Minimal MusicBrainz web service client for recording lookups.
// MusicBrainz allows 1 request/second for anonymous clients and
// requires a meaningful User-Agent (set on the shared HttpClient).
public class MusicBrainzClient
{
    private readonly HttpClient _httpClient;


    public MusicBrainzClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }


    // Returns the earliest release year and the most-voted genre for
    // a recording, either of which may be null if MusicBrainz doesn't
    // know. Null result means the request itself failed (bad id,
    // network, rate limit) - callers can retry those on a later run.
    public async Task<(int? Year, string? Genre)?> LookupRecordingAsync(
        string recordingId)
    {
        var url =
            "https://musicbrainz.org/ws/2/recording/" +
            Uri.EscapeDataString(recordingId) +
            "?inc=releases+genres&fmt=json";

        using var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);

        var root = doc.RootElement;


        // Earliest year across the recording's own first-release-date
        // and all release dates. Dates come as "YYYY", "YYYY-MM", or
        // "YYYY-MM-DD"; the leading 4 digits are all that's needed.

        int? year = ParseYear(
            root.TryGetProperty("first-release-date", out var frd)
                ? frd.GetString()
                : null);

        if (root.TryGetProperty("releases", out var releases) &&
            releases.ValueKind == JsonValueKind.Array)
        {
            foreach (var release in releases.EnumerateArray())
            {
                var releaseYear = ParseYear(
                    release.TryGetProperty("date", out var dateEl)
                        ? dateEl.GetString()
                        : null);

                if (releaseYear != null &&
                    (year == null || releaseYear < year))
                {
                    year = releaseYear;
                }
            }
        }


        // Most-voted genre.

        string? genre = null;

        if (root.TryGetProperty("genres", out var genres) &&
            genres.ValueKind == JsonValueKind.Array)
        {
            var bestCount = -1;

            foreach (var genreElement in genres.EnumerateArray())
            {
                var count = genreElement.TryGetProperty("count", out var countEl)
                    ? countEl.GetInt32()
                    : 0;

                var name = genreElement.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString()
                    : null;

                if (!string.IsNullOrWhiteSpace(name) && count > bestCount)
                {
                    bestCount = count;
                    genre = name;
                }
            }
        }


        return (year, genre);
    }


    private static int? ParseYear(string? date)
    {
        if (string.IsNullOrWhiteSpace(date) || date.Length < 4)
        {
            return null;
        }

        if (int.TryParse(date[..4], out var year) &&
            year is >= 1000 and <= 9999)
        {
            return year;
        }

        return null;
    }
}
