using System.Text.Json;
using TidyTunes.Core.Models;

namespace TidyTunes.Scanner.Services;

public class AcoustIdClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public AcoustIdClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }


    // Looks up a fingerprint against the AcoustID database. Returns null
    // if there's no confident match. meta=recordings+releasegroups+compress
    // asks AcoustID to include MusicBrainz artist/title/album metadata
    // directly in the response, so no separate MusicBrainz call is needed.
    public async Task<AcoustIdMatchResult?> LookupAsync(
        double duration,
        string fingerprint)
    {
        var url =
            "https://api.acoustid.org/v2/lookup" +
            $"?client={Uri.EscapeDataString(_apiKey)}" +
            "&meta=recordings+releasegroups+compress" +
            $"&duration={(int)Math.Round(duration)}" +
            $"&fingerprint={Uri.EscapeDataString(fingerprint)}";

        using var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);

        var root = doc.RootElement;

        if (!root.TryGetProperty("status", out var statusElement) ||
            statusElement.GetString() != "ok")
        {
            return null;
        }

        if (!root.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            return null;
        }


        // Results are returned in descending score order, but pick the
        // highest explicitly rather than assuming.

        JsonElement? best = null;
        var bestScore = -1.0;

        foreach (var result in results.EnumerateArray())
        {
            var score = result.TryGetProperty("score", out var scoreEl)
                ? scoreEl.GetDouble()
                : 0.0;

            if (score > bestScore)
            {
                bestScore = score;
                best = result;
            }
        }

        if (best is null)
        {
            return null;
        }

        var bestResult = best.Value;

        var match = new AcoustIdMatchResult
        {
            AcoustIdId = bestResult.TryGetProperty("id", out var idEl)
                ? idEl.GetString() ?? ""
                : "",
            Score = bestScore
        };


        if (bestResult.TryGetProperty("recordings", out var recordings) &&
            recordings.ValueKind == JsonValueKind.Array &&
            recordings.GetArrayLength() > 0)
        {
            var recording = recordings[0];

            match.RecordingId = recording.TryGetProperty("id", out var recIdEl)
                ? recIdEl.GetString()
                : null;

            match.Title = recording.TryGetProperty("title", out var titleEl)
                ? titleEl.GetString()
                : null;

            if (recording.TryGetProperty("artists", out var artists) &&
                artists.ValueKind == JsonValueKind.Array &&
                artists.GetArrayLength() > 0)
            {
                var names = new List<string>();

                foreach (var artist in artists.EnumerateArray())
                {
                    if (artist.TryGetProperty("name", out var nameEl))
                    {
                        var name = nameEl.GetString();

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            names.Add(name);
                        }
                    }
                }

                if (names.Count > 0)
                {
                    match.Artist = string.Join(", ", names);
                }
            }

            if (recording.TryGetProperty("releasegroups", out var releaseGroups) &&
                releaseGroups.ValueKind == JsonValueKind.Array &&
                releaseGroups.GetArrayLength() > 0)
            {
                var firstGroup = releaseGroups[0];

                match.Album = firstGroup.TryGetProperty("title", out var albumEl)
                    ? albumEl.GetString()
                    : null;
            }
        }

        return match;
    }
}
