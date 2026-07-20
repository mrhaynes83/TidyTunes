namespace TidyTunes.CLI.UI;

public static class ConsoleBanner
{
    public static void Show()
    {
        if (TryShowLogo())
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("              TidyTunes v1.0");

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("        Music Library Management System");

            Console.ResetColor();
            Console.WriteLine();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;

        Console.WriteLine(@"
  _____ _     _      _____
 |_   _(_) __| |_   |_   _|   _ _ __   ___  ___
   | | | |/ _` | | | || || | | | '_ \ / _ \/ __|
   | | | | (_| | |_| || || |_| | | | |  __/\__ \
   |_| |_|\__,_|\__, ||_| \__,_|_| |_|\___||___/
                |___/
");

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("              TidyTunes v1.0");

        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("        Music Library Management System");

        Console.ResetColor();

        Console.WriteLine();
    }


    // Renders the real logo as 24-bit ANSI half-block art (generated
    // from Config\Logo at build time). Only used on a real terminal -
    // piped/redirected output gets the plain ASCII banner instead,
    // since escape sequences would just be noise in a log file.
    private static bool TryShowLogo()
    {
        if (Console.IsOutputRedirected)
        {
            return false;
        }

        var bannerPath = Path.Combine(
            AppContext.BaseDirectory, "Assets", "banner.ans");

        if (!File.Exists(bannerPath))
        {
            return false;
        }

        try
        {
            Console.Write(File.ReadAllText(bannerPath));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
