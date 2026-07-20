namespace TidyTunes.Core.Models;

public class DuplicateGroupResult
{
    public string Hash { get; set; } = string.Empty;

    public List<LibraryFile> Files { get; set; } = new();

    public long FileSize { get; set; }

    // Every copy after the first one is redundant.
    public long WastedBytes =>
        FileSize * (Files.Count - 1);
}
