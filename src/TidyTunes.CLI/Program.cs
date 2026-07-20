using System.Text.Json;
using TidyTunes.CLI.Menus;
using TidyTunes.Core.Models;
using TidyTunes.Data;

// The console defaults to the legacy OEM codepage on Windows; UTF-8
// is needed for the banner's block characters and for music metadata
// with accented characters to print correctly.
try
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
}
catch (Exception)
{
    // Redirected output can reject encoding changes - fine, keep going.
}

Console.WriteLine("================================");
Console.WriteLine("        TidyTunes v1.0");
Console.WriteLine("================================");
Console.WriteLine();

// Walk up from wherever the executable lives until the project's
// Config folder is found, so published builds work from any depth.
var settingsPath = "";

for (var dir = AppContext.BaseDirectory;
     dir != null;
     dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar)))
{
    var candidate = Path.Combine(dir, "Config", "settings.json");

    if (File.Exists(candidate))
    {
        settingsPath = candidate;
        break;
    }
}

if (settingsPath.Length == 0)
{
    settingsPath = Path.Combine(
        AppContext.BaseDirectory, "Config", "settings.json");
}

if (!File.Exists(settingsPath))
{
    Console.WriteLine("ERROR: settings.json not found.");
    Console.WriteLine(settingsPath);
    return;
}

var json = File.ReadAllText(settingsPath);

var settings = JsonSerializer.Deserialize<AppSettings>(
    json,
    new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

if (settings == null)
{
    Console.WriteLine("ERROR: Unable to load settings.");
    return;
}

Console.WriteLine("Configuration loaded.");
Console.WriteLine();

Console.WriteLine($"Music Library:");
Console.WriteLine(settings.MusicLibrary);
Console.WriteLine();

Console.WriteLine($"Database:");
Console.WriteLine(settings.DatabasePath);
Console.WriteLine();

var database = new DatabaseService(settings.DatabasePath);

Console.WriteLine("Testing database connection...");

if (!database.TestConnection())
{
    Console.WriteLine("Database connection failed.");
    return;
}

Console.WriteLine("Database connection successful.");
Console.WriteLine();

var schema = new SchemaManager(database);

Console.WriteLine("Checking database schema...");

schema.EnsureSchema();

Console.WriteLine("Database schema verified.");
Console.WriteLine();

Console.WriteLine("Starting TidyTunes...");

var menu = new MainMenu(
    settings,
    database,
    settingsPath);

menu.Run();