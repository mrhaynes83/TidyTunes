using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TidyTunes.Data;
using TidyTunes.Data.Repositories;

namespace TidyTunes.Analysis;

// Plans and applies the physical organization of the library into
//     <organizedRoot>\Artist\Album\NN - Title.ext
// Files without both Artist and Title stay exactly where they are -
// nothing is ever filed under "Unknown". Plans are persisted to the
// OrganizationPlans table so preview (option 9) and apply (option 10)
// are separate, per the spec's preview-before-modification rule.
public class OrganizationService
{
    private readonly DatabaseService _database;
    private readonly LibraryFileRepository _repository;

    private static readonly char[] InvalidChars =
        Path.GetInvalidFileNameChars()
            .Concat(new[] { ':', '/', '\\', '*', '?', '"', '<', '>', '|' })
            .Distinct()
            .ToArray();


    public OrganizationService(
        DatabaseService database,
        LibraryFileRepository repository)
    {
        _database = database;
        _repository = repository;
    }


    // Builds a fresh plan, replacing any previous un-applied one.
    // template example: {AlbumArtist}\{Album}\{Track} - {Title}
    // Tokens: {Artist} {AlbumArtist} {Album} {Title} {Track} {Year}
    // {Genre}. A folder level whose tokens all come up blank is
    // skipped. Returns (planned, alreadyOrganized, skipped).
    public (long Planned, long AlreadyOrganized, long Skipped) BuildPlan(
        string organizedRoot,
        string? effectsRoot = null,
        string? template = null)
    {
        effectsRoot ??= Path.Combine(
            Path.GetPathRoot(organizedRoot) ?? organizedRoot, "Effects");

        template = string.IsNullOrWhiteSpace(template)
            ? @"{AlbumArtist}\{Album}\{Track} - {Title}"
            : template;
        using var connection = _database.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();


        using (var clear = connection.CreateCommand())
        {
            clear.CommandText =
                "DELETE FROM OrganizationPlans WHERE Status = 'Planned';";

            clear.ExecuteNonQuery();
        }


        var rows = new List<(long Id, string FilePath, string Artist,
            string AlbumArtist, string Album, string Title, int Track,
            string Genre, int Duration, int Year)>();

        using (var read = connection.CreateCommand())
        {
            read.CommandText =
            """
            SELECT
                Id,
                FilePath,
                COALESCE(Artist, ''),
                COALESCE(AlbumArtist, ''),
                COALESCE(Album, ''),
                COALESCE(Title, ''),
                COALESCE(TrackNumber, 0),
                COALESCE(Genre, ''),
                COALESCE(DurationSeconds, 0),
                COALESCE(Year, 0)
            FROM LibraryFiles
            WHERE (IntegrityStatus IS NULL OR IntegrityStatus != 'FileNotFound')
              AND (QuarantinedDate IS NULL OR QuarantinedDate = '');
            """;

            using var reader = read.ExecuteReader();

            while (reader.Read())
            {
                rows.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetInt32(6),
                    reader.GetString(7),
                    reader.GetInt32(8),
                    reader.GetInt32(9)));
            }
        }


        long planned = 0;
        long alreadyOrganized = 0;
        long skipped = 0;

        // Paths claimed by this plan, so two files can't be assigned
        // the same destination.
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var insert = connection.CreateCommand();

        insert.CommandText =
        """
        INSERT INTO OrganizationPlans
            (LibraryFileId, OldPath, NewPath, Status)
        VALUES
            ($fileId, $oldPath, $newPath, 'Planned');
        """;

        var fileIdParam = insert.CreateParameter();
        fileIdParam.ParameterName = "$fileId";
        insert.Parameters.Add(fileIdParam);

        var oldPathParam = insert.CreateParameter();
        oldPathParam.ParameterName = "$oldPath";
        insert.Parameters.Add(oldPathParam);

        var newPathParam = insert.CreateParameter();
        newPathParam.ParameterName = "$newPath";
        insert.Parameters.Add(newPathParam);

        insert.Prepare();


        // Trusted primary artists: names with enough files that a
        // comma/& prefix split can be validated against them, so
        // "2Pac, Big Syke" folds into "2Pac" but "Earth, Wind & Fire"
        // (where "Earth" is no known artist) stays whole.
        var trustedArtists = LoadTrustedArtists(connection);


        // Bootstrap pass: names that only EXIST as split results
        // ("Andrew Spencer" appearing solely inside "Andrew Spencer
        // Vs. Lazard" etc.) become trusted once their resolved files
        // add up, so the second pass can split "& The X" credits
        // against them.

        var resolvedCounts = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var bootstrapCredit = row.AlbumArtist.Length > 0
                ? row.AlbumArtist
                : row.Artist;

            var resolved = ResolvePrimaryArtist(bootstrapCredit, trustedArtists);

            if (resolved.Length > 0)
            {
                resolvedCounts[resolved] =
                    resolvedCounts.GetValueOrDefault(resolved) + 1;
            }
        }

        foreach (var (name, count) in resolvedCounts)
        {
            if (count >= 3 && !name.Contains(','))
            {
                trustedArtists.Add(name.ToLowerInvariant());
            }
        }

        // Canonical display name per folded key: different featuring
        // credits resolve to different spellings of the same act
        // ("Khriz & Angel" / "Ángel y Khriz") - the spelling with the
        // most files names the folder for all of them.

        var canonicalByKey = new Dictionary<string, (string Name, int Count)>();

        foreach (var (name, count) in resolvedCounts)
        {
            var key = Core.ArtistNameFolding.FoldKey(name);

            if (key.Length == 0)
            {
                continue;
            }

            if (!canonicalByKey.TryGetValue(key, out var best)
                || count > best.Count)
            {
                canonicalByKey[key] = (name, count);
            }
        }


        foreach (var row in rows)
        {
            var credit = row.AlbumArtist.Length > 0 ? row.AlbumArtist : row.Artist;

            var artist = ResolvePrimaryArtist(credit, trustedArtists);

            if (artist.Length > 0)
            {
                var canonicalKey = Core.ArtistNameFolding.FoldKey(artist);

                if (canonicalKey.Length > 0
                    && canonicalByKey.TryGetValue(canonicalKey, out var canonical))
                {
                    artist = canonical.Name;
                }
            }

            if (artist.Length == 0 || row.Title.Length == 0)
            {
                // Unorganizable files normally stay where they are -
                // but if a previous run filed them under a junk artist
                // folder inside Organized, they move to _Unsorted so
                // the artist tree stays clean.

                var organizedPrefix = organizedRoot + Path.DirectorySeparatorChar;

                var unsortedRoot = Path.Combine(organizedRoot, "_Unsorted");

                if (row.FilePath.StartsWith(organizedPrefix, StringComparison.OrdinalIgnoreCase)
                    && !row.FilePath.StartsWith(unsortedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var rescueName = Path.GetFileName(row.FilePath);

                    var rescueBase = Path.GetFileNameWithoutExtension(rescueName);

                    var rescueExtension = Path.GetExtension(rescueName);

                    string? rescueTarget = null;

                    for (var i = 1; i <= 99; i++)
                    {
                        var candidate = Path.Combine(
                            unsortedRoot,
                            i == 1
                                ? rescueName
                                : $"{rescueBase} ({i}){rescueExtension}");

                        if (claimed.Add(candidate) && !File.Exists(candidate))
                        {
                            rescueTarget = candidate;
                            break;
                        }
                    }

                    if (rescueTarget != null)
                    {
                        fileIdParam.Value = row.Id;
                        oldPathParam.Value = row.FilePath;
                        newPathParam.Value = rescueTarget;

                        insert.ExecuteNonQuery();

                        planned++;
                        continue;
                    }
                }

                skipped++;
                continue;
            }

            var extension = Path.GetExtension(row.FilePath);

            // Bucket layer: Electronic\<subtype> or International slot
            // in between the root and the artist. Effects live OUTSIDE
            // the music tree entirely, at the effects root.

            var bucket = ResolveBucket(row.Genre, row.Duration, row.Title);

            string bucketRoot;

            if (bucket.StartsWith("Effects", StringComparison.Ordinal))
            {
                var sub = bucket.Length > "Effects".Length
                    ? bucket["Effects".Length..].TrimStart(Path.DirectorySeparatorChar)
                    : "";

                bucketRoot = Path.Combine(effectsRoot, sub);
            }
            else
            {
                bucketRoot = bucket.Length > 0
                    ? Path.Combine(organizedRoot, bucket)
                    : organizedRoot;
            }

            var relative = RenderTemplate(
                template, artist, row.Album, row.Title, row.Track,
                row.Genre, row.Year, extension);

            if (relative == null)
            {
                skipped++;
                continue;
            }

            var directory = Path.Combine(
                bucketRoot,
                Path.GetDirectoryName(relative) ?? "");

            var fileName = Path.GetFileName(relative);

            var target = Path.Combine(directory, fileName);

            if (string.Equals(target, row.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                claimed.Add(target);
                alreadyOrganized++;
                continue;
            }

            // Resolve collisions against already-claimed targets and
            // files already sitting at the destination. A file already
            // parked at one of the collision-suffixed names stays put -
            // replanning must be idempotent, not shuffle suffixes.

            if (!claimed.Add(target) || File.Exists(target))
            {
                var baseName = Path.GetFileNameWithoutExtension(fileName);

                var resolved = false;
                var stayedPut = false;

                for (var i = 2; i <= 99; i++)
                {
                    var candidate = Path.Combine(
                        directory, $"{baseName} ({i}){extension}");

                    if (string.Equals(candidate, row.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        claimed.Add(candidate);
                        alreadyOrganized++;
                        stayedPut = true;
                        break;
                    }

                    if (claimed.Add(candidate) && !File.Exists(candidate))
                    {
                        target = candidate;
                        resolved = true;
                        break;
                    }
                }

                if (stayedPut)
                {
                    continue;
                }

                if (!resolved)
                {
                    skipped++;
                    continue;
                }
            }

            fileIdParam.Value = row.Id;
            oldPathParam.Value = row.FilePath;
            newPathParam.Value = target;

            insert.ExecuteNonQuery();

            planned++;
        }

        transaction.Commit();

        return (planned, alreadyOrganized, skipped);
    }


    public List<(string OldPath, string NewPath)> GetPlanSamples(int count)
    {
        var samples = new List<(string, string)>();

        using var connection = _database.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT OldPath, NewPath
        FROM OrganizationPlans
        WHERE Status = 'Planned'
        LIMIT $count;
        """;

        command.Parameters.AddWithValue("$count", count);

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            samples.Add((reader.GetString(0), reader.GetString(1)));
        }

        return samples;
    }


    // Applies the planned moves: move file, update the library row,
    // mark the plan row Applied (or Failed with the reason).
    public OrganizationResult ApplyPlan()
    {
        var result = new OrganizationResult();

        var plans = new List<(long PlanId, long FileId, string OldPath, string NewPath)>();

        using (var connection = _database.GetConnection())
        {
            connection.Open();

            using var read = connection.CreateCommand();

            read.CommandText =
            """
            SELECT Id, LibraryFileId, OldPath, NewPath
            FROM OrganizationPlans
            WHERE Status = 'Planned';
            """;

            using var reader = read.ExecuteReader();

            while (reader.Read())
            {
                plans.Add((
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetString(3)));
            }
        }

        Console.WriteLine($"Applying {plans.Count:N0} planned move(s)...");
        Console.WriteLine();


        var pathUpdates = new List<(long Id, string NewPath, string NewFileName)>();

        var planOutcomes = new List<(long PlanId, string Status)>();

        var processed = 0;

        foreach (var plan in plans)
        {
            processed++;

            try
            {
                if (!File.Exists(plan.OldPath))
                {
                    planOutcomes.Add((plan.PlanId, "Failed:SourceMissing"));
                    result.Failed++;
                    continue;
                }

                if (File.Exists(plan.NewPath))
                {
                    planOutcomes.Add((plan.PlanId, "Failed:TargetExists"));
                    result.Failed++;
                    continue;
                }

                Directory.CreateDirectory(
                    Path.GetDirectoryName(plan.NewPath)!);

                File.Move(plan.OldPath, plan.NewPath);

                pathUpdates.Add((
                    plan.FileId,
                    plan.NewPath,
                    Path.GetFileName(plan.NewPath)));

                planOutcomes.Add((plan.PlanId, "Applied"));

                result.Moved++;
            }
            catch (Exception ex)
            {
                planOutcomes.Add((plan.PlanId, $"Failed:{ex.GetType().Name}"));
                result.Failed++;
            }


            if (pathUpdates.Count >= 500)
            {
                _repository.UpdateFilePathsBatch(pathUpdates);
                pathUpdates.Clear();

                MarkPlans(planOutcomes);
                planOutcomes.Clear();
            }

            if (processed % 1000 == 0)
            {
                Console.WriteLine(
                    $"[{processed:N0}/{plans.Count:N0}] moved {result.Moved:N0}, " +
                    $"failed {result.Failed:N0}...");
            }
        }

        if (pathUpdates.Count > 0)
        {
            _repository.UpdateFilePathsBatch(pathUpdates);
        }

        if (planOutcomes.Count > 0)
        {
            MarkPlans(planOutcomes);
        }

        return result;
    }


    private void MarkPlans(List<(long PlanId, string Status)> outcomes)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.CommandText =
            "UPDATE OrganizationPlans SET Status = $status WHERE Id = $id;";

        var statusParam = command.CreateParameter();
        statusParam.ParameterName = "$status";
        command.Parameters.Add(statusParam);

        var idParam = command.CreateParameter();
        idParam.ParameterName = "$id";
        command.Parameters.Add(idParam);

        command.Prepare();

        foreach (var (planId, status) in outcomes)
        {
            statusParam.Value = status;
            idParam.Value = planId;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }


    // Turns the user template into a sanitized relative path. Levels
    // whose tokens all resolved blank are dropped; a blank filename
    // falls back to the title. Returns null when nothing usable
    // remains (caller skips the file).
    private static string? RenderTemplate(
        string template,
        string artist,
        string album,
        string title,
        int track,
        string genre,
        int year,
        string extension)
    {
        var tokens = new (string Token, string Value)[]
        {
            ("{Artist}", artist),
            ("{AlbumArtist}", artist),
            ("{Album}", album),
            ("{Title}", title),
            ("{Track}", track > 0 ? track.ToString("00") : ""),
            ("{Year}", year > 0 ? year.ToString() : ""),
            ("{Genre}", genre),
        };

        var segments = template.Split(
            new[] { '\\', '/' },
            StringSplitOptions.RemoveEmptyEntries);

        var parts = new List<string>();

        foreach (var segment in segments)
        {
            var rendered = segment;

            var hadToken = false;
            var hadValue = false;

            foreach (var (token, value) in tokens)
            {
                if (!rendered.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                hadToken = true;

                if (value.Length > 0)
                {
                    hadValue = true;
                }

                rendered = Regex.Replace(
                    rendered,
                    Regex.Escape(token),
                    Sanitize(value).Replace("$", "$$"),
                    RegexOptions.IgnoreCase);
            }

            if (hadToken && !hadValue)
            {
                continue;   // e.g. no album -> drop the album level
            }

            rendered = Regex.Replace(rendered, @"\s+", " ")
                .Trim(' ', '-', '_', '.');

            if (rendered.Length > 0)
            {
                parts.Add(rendered);
            }
        }

        if (parts.Count == 0)
        {
            var fallback = Sanitize(title);

            if (fallback.Length == 0 || fallback == "Unknown")
            {
                return null;
            }

            parts.Add(fallback);
        }

        parts[^1] += extension;

        return Path.Combine(parts.ToArray());
    }


    // Genre subtype mapping for the Electronic bucket - most specific
    // first, so "Tech House" lands in House, not Techno.
    private static readonly (Regex Pattern, string Subtype)[] ElectronicSubtypes =
    {
        (new Regex(@"\bhouse\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "House"),
        (new Regex(@"\btechno\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Techno"),
        (new Regex(@"\btrance\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Trance"),
        (new Regex(@"\bdubstep\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Dubstep"),
        (new Regex(@"\b(drum\s*(&|and)\s*bass|dnb|d&b|jungle)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Drum & Bass"),
        (new Regex(@"\bhardstyle\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Hardstyle"),
        (new Regex(@"\b(happy\s+hardcore|gabber)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Hardcore"),
        (new Regex(@"\b(breakbeat|big\s+beat)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Breakbeat"),
        // "dance" must not catch "dancehall".
        (new Regex(@"\b(edm|electro(nic(a)?)?|eurodance|big\s+room|future\s+bass|uk\s+garage|rave|club)\b|(?<!hall.{0,10})\bdance\b(?!\s*hall|hall)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "EDM"),
    };

    private static readonly Regex InternationalGenre = new(
        @"\b(latin(o|a)?( pop)?|reggaet[oó]n|bachata|salsa|merengue|cumbia|dembow|banda|norte[ñn]o|corrido|mariachi|flamenco|tropical|world|afrobeats?|soca|kompa|bollywood|bhangra|k-?pop|j-?pop|mandopop|cantopop|schlager|chanson|italo\s+pop)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GagKeywords = new(
        @"\b(gag|prank|fart|burp|belch|joke)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuoteKeywords = new(
        @"\b(movie\s+quote|film\s+quote|quote|dialogue|dialog|scene)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EffectKeywordsStrong = new(
        @"\b(sound\s*effects?|sfx|dj\s+drop|air\s*horn|siren|jingle)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EffectKeywordsWeak = new(
        @"\b(effects?|drops?|skit|intro|outro)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);


    // Decides which bucket a file belongs to. Precedence: Effects
    // (function beats genre), then Electronic, then International;
    // empty string means the normal artist tree.
    private static string ResolveBucket(string genre, int durationSeconds, string title)
    {
        var isShort = durationSeconds > 0 && durationSeconds <= 60;

        // Effects: strong keywords always count; weak ones (or a
        // short Soundtrack cut) only for very short files.

        var effectMatch =
            GagKeywords.IsMatch(title) ||
            QuoteKeywords.IsMatch(title) ||
            EffectKeywordsStrong.IsMatch(title) ||
            (isShort && EffectKeywordsWeak.IsMatch(title)) ||
            (isShort && Regex.IsMatch(genre, @"\b(soundtrack|sound\s*effects?|sfx)\b", RegexOptions.IgnoreCase));

        if (effectMatch)
        {
            if (GagKeywords.IsMatch(title))
            {
                return Path.Combine("Effects", "Gag Effects");
            }

            if (QuoteKeywords.IsMatch(title)
                || Regex.IsMatch(genre, @"\bsoundtrack\b", RegexOptions.IgnoreCase))
            {
                return Path.Combine("Effects", "Movie Quotes");
            }

            return Path.Combine("Effects", "Sound Effects");
        }

        if (genre.Length > 0
            && !Regex.IsMatch(genre, @"\bdancehall\b", RegexOptions.IgnoreCase))
        {
            foreach (var (pattern, subtype) in ElectronicSubtypes)
            {
                if (pattern.IsMatch(genre))
                {
                    return Path.Combine("Electronic", subtype);
                }
            }
        }

        if (genre.Length > 0 && InternationalGenre.IsMatch(genre))
        {
            return "International";
        }

        return "";
    }


    // Featuring markers always split - they are never part of a band
    // name. Comma/& splits are only taken when the prefix is a
    // trusted artist in this library.
    private static readonly Regex FeaturingMarker = new(
        @"\s+(feat\.?|featuring|ft\.?|f\.?|vs\.?|versus|meets)\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);


    private static string ResolvePrimaryArtist(
        string credit,
        HashSet<string> trustedArtists)
    {
        var primary = credit.Trim();

        var featuring = FeaturingMarker.Match(primary);

        if (featuring.Success && featuring.Index > 0)
        {
            primary = primary[..featuring.Index].Trim();
        }

        // "2Pac, Big Syke": commas separate credit lists far more
        // reliably than "&" (which lives inside band names), so comma
        // prefixes are tried first - longest first, letting
        // "Chaka Demus & Pliers, Jack Radics" fold to the duo rather
        // than a single member.

        for (var i = primary.Length - 1; i > 0; i--)
        {
            if (primary[i] != ',')
            {
                continue;
            }

            var prefix = primary[..i].Trim();

            if (trustedArtists.Contains(prefix.ToLowerInvariant()))
            {
                return prefix;
            }
        }

        // "&" / " and " splits are the last resort, and never in front
        // of "His/Her/..." - "Bill Haley & His Comets" is one band.
        // "& The X" DOES split when the prefix is trusted, so
        // "Andrew Spencer & The Vamprockers" folds into the artist
        // the library already knows.

        var joinMatch = Regex.Match(primary, @"\s*&\s*|\s+and\s+",
            RegexOptions.IgnoreCase);

        if (joinMatch.Success && joinMatch.Index > 0)
        {
            var after = primary[(joinMatch.Index + joinMatch.Length)..]
                .TrimStart().ToLowerInvariant();

            var bandConstruction =
                after.StartsWith("his ") || after.StartsWith("her ") ||
                after.StartsWith("friends");

            if (!bandConstruction)
            {
                var prefix = primary[..joinMatch.Index].Trim();

                if (trustedArtists.Contains(prefix.ToLowerInvariant()))
                {
                    return prefix;
                }
            }
        }

        return primary.Trim(' ', ',', '&');
    }


    private static HashSet<string> LoadTrustedArtists(
        SqliteConnection connection)
    {
        var trusted = new HashSet<string>(StringComparer.Ordinal);

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT Artist FROM LibraryFiles
        WHERE Artist IS NOT NULL AND Artist != ''
        GROUP BY Artist
        HAVING COUNT(*) >= 3;
        """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var artist = reader.GetString(0).Trim();

            // Comma-carrying names can't be trusted split targets
            // (they are themselves credit lists more often than not),
            // but "&" names like "Chaka Demus & Pliers" are usually
            // real duos and make valid targets.
            if (artist.Length >= 2 && !artist.Contains(','))
            {
                trusted.Add(artist.ToLowerInvariant());
            }
        }

        return trusted;
    }


    // Renames ALL-CAPS artist/album folders to Title Case on disk
    // (two-step rename, since Windows treats the names as equal) and
    // rewrites the affected FilePaths so the database stays exact.
    public int FixDirectoryCasing(string root)
    {
        var renamed = 0;

        if (!Directory.Exists(root))
        {
            return renamed;
        }

        // Album folders first (deepest), then artist folders.
        var directories = Directory
            .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.Length)
            .ToList();

        foreach (var directory in directories)
        {
            var name = Path.GetFileName(directory);

            if (!Core.TextCasing.IsAllCapsMultiWord(name))
            {
                continue;
            }

            var fixedName = Core.TextCasing.ToTitleCase(name);

            if (fixedName == name)
            {
                continue;
            }

            var parent = Path.GetDirectoryName(directory)!;

            var target = Path.Combine(parent, fixedName);

            var temp = Path.Combine(parent, fixedName + ".casefix~");

            try
            {
                Directory.Move(directory, temp);
                Directory.Move(temp, target);
            }
            catch (Exception)
            {
                continue;
            }

            UpdatePathPrefix(directory, target);

            renamed++;
        }

        return renamed;
    }


    private void UpdatePathPrefix(string oldPrefix, string newPrefix)
    {
        using var connection = _database.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET FilePath = $newPrefix || SUBSTR(FilePath, LENGTH($oldPrefix) + 1)
        WHERE SUBSTR(FilePath, 1, LENGTH($oldPrefix)) = $oldPrefix;
        """;

        command.Parameters.AddWithValue("$oldPrefix", oldPrefix + Path.DirectorySeparatorChar);
        command.Parameters.AddWithValue("$newPrefix", newPrefix + Path.DirectorySeparatorChar);

        command.ExecuteNonQuery();
    }


    // Removes directories left empty after consolidation moves.
    public int CleanEmptyDirectories(string root)
    {
        var removed = 0;

        if (!Directory.Exists(root))
        {
            return removed;
        }

        foreach (var directory in Directory
            .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                    removed++;
                }
            }
            catch (Exception)
            {
                // A locked or freshly-filled folder just stays.
            }
        }

        return removed;
    }


    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            builder.Append(InvalidChars.Contains(c) ? '_' : c);
        }

        var sanitized = builder.ToString().Trim(' ', '.');

        if (sanitized.Length > 100)
        {
            sanitized = sanitized[..100].Trim(' ', '.');
        }

        return sanitized.Length == 0 ? "Unknown" : sanitized;
    }


    public string WriteLog(
        string logsDirectory,
        OrganizationResult result,
        DateTime startTime)
    {
        Directory.CreateDirectory(logsDirectory);

        var logPath = Path.Combine(
            logsDirectory,
            $"Organization_{startTime:yyyyMMdd_HHmmss}.log");

        var builder = new StringBuilder();

        builder.AppendLine("Organization Apply");
        builder.AppendLine($"Started:     {startTime:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Finished:    {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine($"Files moved: {result.Moved:N0}");
        builder.AppendLine($"Failed:      {result.Failed:N0}");
        builder.AppendLine();
        builder.AppendLine(
            "Full per-file outcomes are in the OrganizationPlans table.");

        File.WriteAllText(logPath, builder.ToString());

        return logPath;
    }
}


public class OrganizationResult
{
    public long Moved { get; set; }

    public long Failed { get; set; }
}
