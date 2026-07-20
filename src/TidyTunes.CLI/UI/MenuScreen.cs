namespace TidyTunes.CLI.UI;

public class MenuItem
{
    public string Key { get; init; } = "";

    public string Title { get; init; } = "";

    // Shown in the right-hand column of the menu.
    public string Description { get; init; } = "";

    // Shown on the pre-action screen: exactly what is about to happen.
    public string[] Details { get; init; } = Array.Empty<string>();

    // Non-empty marks the action destructive/unrecoverable; each line
    // is listed under the DANGER banner and YES is required to proceed.
    public string[] Dangers { get; init; } = Array.Empty<string>();

    public Action Handler { get; init; } = () => { };
}


public static class MenuScreen
{
    public static void Render(
        IReadOnlyList<(string Section, MenuItem[] Items)> sections)
    {
        var width = 37;

        foreach (var (section, items) in sections)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(section);
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(new string('-', section.Length));
            Console.ResetColor();

            foreach (var item in items)
            {
                var left = $"{item.Key,3}. {item.Title}";

                if (left.Length > width)
                {
                    left = left[..width];
                }

                Console.Write(left.PadRight(width));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(item.Description);
                Console.ResetColor();
            }

            Console.WriteLine();
        }
    }


    // Pre-action screen: explains what is about to happen; destructive
    // actions additionally show a DANGER banner and require YES.
    // Returns true when the user chose to proceed.
    public static bool Confirm(MenuItem item)
    {
        if (item.Details.Length == 0 && item.Dangers.Length == 0)
        {
            return true;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(item.Title);
        Console.WriteLine(new string('=', item.Title.Length));
        Console.ResetColor();

        if (item.Details.Length > 0)
        {
            Console.WriteLine();
            Console.WriteLine("What this will do:");

            foreach (var line in item.Details)
            {
                Console.WriteLine($"  - {line}");
            }
        }

        if (item.Dangers.Length > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  !!! DANGER - READ BEFORE PROCEEDING !!!");
            Console.ResetColor();

            foreach (var line in item.Dangers)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ! {line}");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.Write("Type YES (all caps) to proceed, anything else cancels: ");

            var answer = Console.ReadLine();

            if (!string.Equals(answer?.Trim(), "YES", StringComparison.Ordinal))
            {
                Console.WriteLine("Cancelled. Nothing was changed.");
                return false;
            }

            return true;
        }

        Console.WriteLine();
        Console.Write("Press ENTER to continue, or Q to go back: ");

        var choice = Console.ReadLine();

        return !string.Equals(choice?.Trim(), "q", StringComparison.OrdinalIgnoreCase);
    }
}
