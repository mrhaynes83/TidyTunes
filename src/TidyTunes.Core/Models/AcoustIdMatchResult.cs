namespace TidyTunes.Core.Models;

public class AcoustIdMatchResult
{
    public string AcoustIdId { get; set; } = string.Empty;

    public double Score { get; set; }

    public string? RecordingId { get; set; }

    public string? Title { get; set; }

    public string? Artist { get; set; }

    public string? Album { get; set; }
}
