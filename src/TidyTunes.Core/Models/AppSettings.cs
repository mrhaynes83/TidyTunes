namespace TidyTunes.Core.Models;

public class AppSettings
{
    public string MusicLibrary { get; set; } = string.Empty;

    public string DatabasePath { get; set; } = string.Empty;

    public string DuplicateHandling { get; set; } = "KeepBest";

    public string CorruptFileHandling { get; set; } = "Quarantine";

    public bool RequireDeleteConfirmation { get; set; } = true;

    public string AcoustIdApiKey { get; set; } = string.Empty;

    public string FpCalcPath { get; set; } = string.Empty;

    public string FfmpegPath { get; set; } = string.Empty;

    // Where organized music lands; defaults to <MusicLibrary>\Organized.
    public string OrganizedRoot { get; set; } = string.Empty;

    // Where sound effects / quotes land; defaults to <drive>\Effects.
    public string EffectsRoot { get; set; } = string.Empty;

    // Layout for organized files. Tokens: {Artist} {AlbumArtist}
    // {Album} {Title} {Track} {Year} {Genre}. Path separators split
    // folder levels; a level whose tokens are all blank is skipped.
    public string OrganizationTemplate { get; set; } =
        @"{AlbumArtist}\{Album}\{Track} - {Title}";

    // How many database backups to keep (oldest pruned). 0 = unlimited.
    public int BackupRetention { get; set; } = 3;

    // Where backups go; defaults to <project>\Data\Backups.
    public string BackupsPath { get; set; } = string.Empty;


    // Alias used by scanner services; excluded from settings.json so
    // the value isn't written twice.
    [System.Text.Json.Serialization.JsonIgnore]
    public string MusicPath
    {
        get => MusicLibrary;
        set => MusicLibrary = value;
    }
}