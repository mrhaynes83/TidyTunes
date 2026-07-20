using Microsoft.Data.Sqlite;
using TidyTunes.Core.Models;

namespace TidyTunes.Data.Repositories;

public class LibraryFileRepository
{
    private readonly DatabaseService _databaseService;


    public LibraryFileRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }


    public void AddOrUpdate(LibraryFile file)
    {
        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        INSERT INTO LibraryFiles
        (
            FilePath,
            FileName,
            Extension,
            FileSize,
            ModifiedDate,

            Artist,
            Album,
            Title,

            Md5Hash,
            Sha256Hash,

            FingerprintStatus,
            FingerprintGeneratedDate,
            AcoustIdConfidence,

            MusicBrainzStatus,
            MetadataConfidence,

            FileHash,
            AudioHash,

            DurationSeconds,
            BitRate,
            SampleRate,
            Channels,

            DateAdded,
            LastMetadataRefresh
        )
        VALUES
        (
            $FilePath,
            $FileName,
            $Extension,
            $FileSize,
            $ModifiedDate,

            $Artist,
            $Album,
            $Title,

            $Md5Hash,
            $Sha256Hash,

            $FingerprintStatus,
            $FingerprintGeneratedDate,
            $AcoustIdConfidence,

            $MusicBrainzStatus,
            $MetadataConfidence,

            $FileHash,
            $AudioHash,

            $DurationSeconds,
            $BitRate,
            $SampleRate,
            $Channels,

            $DateAdded,
            $LastMetadataRefresh
        )

        ON CONFLICT(FilePath)
        DO UPDATE SET

            FileName = excluded.FileName,
            Extension = excluded.Extension,
            FileSize = excluded.FileSize,
            ModifiedDate = excluded.ModifiedDate,

            Artist = excluded.Artist,
            Album = excluded.Album,
            Title = excluded.Title,

            Md5Hash = excluded.Md5Hash,
            Sha256Hash = excluded.Sha256Hash,

            FingerprintStatus = excluded.FingerprintStatus,
            FingerprintGeneratedDate = excluded.FingerprintGeneratedDate,
            AcoustIdConfidence = excluded.AcoustIdConfidence,

            MusicBrainzStatus = excluded.MusicBrainzStatus,
            MetadataConfidence = excluded.MetadataConfidence,

            FileHash = excluded.FileHash,
            AudioHash = excluded.AudioHash,

            DurationSeconds = excluded.DurationSeconds,
            BitRate = excluded.BitRate,
            SampleRate = excluded.SampleRate,
            Channels = excluded.Channels,

            LastMetadataRefresh = excluded.LastMetadataRefresh;
        """;


        command.Parameters.AddWithValue("$FilePath", file.FilePath);
        command.Parameters.AddWithValue("$FileName", file.FileName ?? "");
        command.Parameters.AddWithValue("$Extension", file.Extension ?? "");
        command.Parameters.AddWithValue("$FileSize", file.FileSize);
        command.Parameters.AddWithValue("$ModifiedDate", file.ModifiedDate?.ToString() ?? "");

        command.Parameters.AddWithValue("$Artist", file.Artist ?? "");
        command.Parameters.AddWithValue("$Album", file.Album ?? "");
        command.Parameters.AddWithValue("$Title", file.Title ?? "");

        command.Parameters.AddWithValue("$Md5Hash", file.Md5Hash ?? "");
        command.Parameters.AddWithValue("$Sha256Hash", file.Sha256Hash ?? "");

        command.Parameters.AddWithValue("$FingerprintStatus", file.FingerprintStatus ?? "Pending");
        command.Parameters.AddWithValue("$FingerprintGeneratedDate", file.FingerprintGeneratedDate?.ToString() ?? "");
        command.Parameters.AddWithValue("$AcoustIdConfidence", file.AcoustIdConfidence);

        command.Parameters.AddWithValue("$MusicBrainzStatus", file.MusicBrainzStatus ?? "Pending");
        command.Parameters.AddWithValue("$MetadataConfidence", file.MetadataConfidence);

        command.Parameters.AddWithValue("$FileHash", file.FileHash ?? "");
        command.Parameters.AddWithValue("$AudioHash", file.AudioHash ?? "");

        command.Parameters.AddWithValue("$DurationSeconds", file.DurationSeconds ?? 0);
        command.Parameters.AddWithValue("$BitRate", file.BitRate ?? 0);
        command.Parameters.AddWithValue("$SampleRate", file.SampleRate ?? 0);
        command.Parameters.AddWithValue("$Channels", file.Channels ?? 0);

        command.Parameters.AddWithValue("$DateAdded", file.DateAdded.ToString());
        command.Parameters.AddWithValue("$LastMetadataRefresh", file.LastMetadataRefresh?.ToString() ?? "");

        command.ExecuteNonQuery();
    }


    // Same INSERT/UPDATE as AddOrUpdate, but for many files in a single
    // transaction. Use this for bulk operations (like scanning a whole
    // folder) instead of calling AddOrUpdate() in a loop, since one
    // transaction per file gets very slow at scale.
    public void AddOrUpdateBatch(List<LibraryFile> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        INSERT INTO LibraryFiles
        (
            FilePath,
            FileName,
            Extension,
            FileSize,
            ModifiedDate,

            Artist,
            Album,
            Title,

            Md5Hash,
            Sha256Hash,

            FingerprintStatus,
            FingerprintGeneratedDate,
            AcoustIdConfidence,

            MusicBrainzStatus,
            MetadataConfidence,

            FileHash,
            AudioHash,

            DurationSeconds,
            BitRate,
            SampleRate,
            Channels,

            DateAdded,
            LastMetadataRefresh
        )
        VALUES
        (
            $FilePath,
            $FileName,
            $Extension,
            $FileSize,
            $ModifiedDate,

            $Artist,
            $Album,
            $Title,

            $Md5Hash,
            $Sha256Hash,

            $FingerprintStatus,
            $FingerprintGeneratedDate,
            $AcoustIdConfidence,

            $MusicBrainzStatus,
            $MetadataConfidence,

            $FileHash,
            $AudioHash,

            $DurationSeconds,
            $BitRate,
            $SampleRate,
            $Channels,

            $DateAdded,
            $LastMetadataRefresh
        )

        ON CONFLICT(FilePath)
        DO UPDATE SET

            FileName = excluded.FileName,
            Extension = excluded.Extension,
            FileSize = excluded.FileSize,
            ModifiedDate = excluded.ModifiedDate,

            Artist = excluded.Artist,
            Album = excluded.Album,
            Title = excluded.Title,

            Md5Hash = excluded.Md5Hash,
            Sha256Hash = excluded.Sha256Hash,

            FingerprintStatus = excluded.FingerprintStatus,
            FingerprintGeneratedDate = excluded.FingerprintGeneratedDate,
            AcoustIdConfidence = excluded.AcoustIdConfidence,

            MusicBrainzStatus = excluded.MusicBrainzStatus,
            MetadataConfidence = excluded.MetadataConfidence,

            FileHash = excluded.FileHash,
            AudioHash = excluded.AudioHash,

            DurationSeconds = excluded.DurationSeconds,
            BitRate = excluded.BitRate,
            SampleRate = excluded.SampleRate,
            Channels = excluded.Channels,

            LastMetadataRefresh = excluded.LastMetadataRefresh;
        """;

        AddParam(command, "$FilePath");
        AddParam(command, "$FileName");
        AddParam(command, "$Extension");
        AddParam(command, "$FileSize");
        AddParam(command, "$ModifiedDate");
        AddParam(command, "$Artist");
        AddParam(command, "$Album");
        AddParam(command, "$Title");
        AddParam(command, "$Md5Hash");
        AddParam(command, "$Sha256Hash");
        AddParam(command, "$FingerprintStatus");
        AddParam(command, "$FingerprintGeneratedDate");
        AddParam(command, "$AcoustIdConfidence");
        AddParam(command, "$MusicBrainzStatus");
        AddParam(command, "$MetadataConfidence");
        AddParam(command, "$FileHash");
        AddParam(command, "$AudioHash");
        AddParam(command, "$DurationSeconds");
        AddParam(command, "$BitRate");
        AddParam(command, "$SampleRate");
        AddParam(command, "$Channels");
        AddParam(command, "$DateAdded");
        AddParam(command, "$LastMetadataRefresh");

        command.Prepare();

        foreach (var file in files)
        {
            command.Parameters["$FilePath"].Value = file.FilePath;
            command.Parameters["$FileName"].Value = file.FileName ?? "";
            command.Parameters["$Extension"].Value = file.Extension ?? "";
            command.Parameters["$FileSize"].Value = file.FileSize;
            command.Parameters["$ModifiedDate"].Value = file.ModifiedDate?.ToString() ?? "";

            command.Parameters["$Artist"].Value = file.Artist ?? "";
            command.Parameters["$Album"].Value = file.Album ?? "";
            command.Parameters["$Title"].Value = file.Title ?? "";

            command.Parameters["$Md5Hash"].Value = file.Md5Hash ?? "";
            command.Parameters["$Sha256Hash"].Value = file.Sha256Hash ?? "";

            command.Parameters["$FingerprintStatus"].Value = file.FingerprintStatus ?? "Pending";
            command.Parameters["$FingerprintGeneratedDate"].Value = file.FingerprintGeneratedDate?.ToString() ?? "";
            command.Parameters["$AcoustIdConfidence"].Value = file.AcoustIdConfidence;

            command.Parameters["$MusicBrainzStatus"].Value = file.MusicBrainzStatus ?? "Pending";
            command.Parameters["$MetadataConfidence"].Value = file.MetadataConfidence;

            command.Parameters["$FileHash"].Value = file.FileHash ?? "";
            command.Parameters["$AudioHash"].Value = file.AudioHash ?? "";

            command.Parameters["$DurationSeconds"].Value = file.DurationSeconds ?? 0;
            command.Parameters["$BitRate"].Value = file.BitRate ?? 0;
            command.Parameters["$SampleRate"].Value = file.SampleRate ?? 0;
            command.Parameters["$Channels"].Value = file.Channels ?? 0;

            command.Parameters["$DateAdded"].Value = file.DateAdded.ToString();
            command.Parameters["$LastMetadataRefresh"].Value = file.LastMetadataRefresh?.ToString() ?? "";

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }


    // Generic filtered listing used by Search & Filter and exports.
    // whereSql is trusted caller-built SQL with $p0..$pN parameters.
    public List<LibraryFile> QueryFiles(
        string whereSql,
        object[] args,
        int limit = 0)
    {
        var files = new List<LibraryFile>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT Id, FilePath, FileName, Artist, Album, Title, Genre, " +
            "Year, BitRate, DurationSeconds, IntegrityStatus, Bpm, MusicalKey " +
            "FROM LibraryFiles WHERE " + whereSql +
            " ORDER BY Artist, Album, Title" +
            (limit > 0 ? $" LIMIT {limit}" : "");

        for (var i = 0; i < args.Length; i++)
        {
            command.Parameters.AddWithValue($"$p{i}", args[i]);
        }

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            files.Add(new LibraryFile
            {
                Id = reader.GetInt64(0),
                FilePath = reader.GetString(1),
                FileName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Artist = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Album = reader.IsDBNull(4) ? "" : reader.GetString(4),
                Title = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Genre = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Year = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                BitRate = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                DurationSeconds = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                IntegrityStatus = reader.IsDBNull(10) ? null : reader.GetString(10),
                Bpm = reader.IsDBNull(11) ? null : reader.GetInt32(11),
                MusicalKey = reader.IsDBNull(12) ? null : reader.GetString(12)
            });
        }

        return files;
    }


    public long CountFiles(string whereSql, object[] args)
    {
        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT COUNT(*) FROM LibraryFiles WHERE " + whereSql;

        for (var i = 0; i < args.Length; i++)
        {
            command.Parameters.AddWithValue($"$p{i}", args[i]);
        }

        return (long)(command.ExecuteScalar() ?? 0L);
    }


    private static void AddParam(SqliteCommand command, string name)
    {
        var param = command.CreateParameter();
        param.ParameterName = name;
        command.Parameters.Add(param);
    }
    public void Add(LibraryFile file)
    {
        AddOrUpdate(file);
    }


    public List<LibraryFile> GetAll()
    {
        var files = new List<LibraryFile>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            Id,
            FilePath,
            FileName,
            Extension,
            Artist,
            Album,
            Title
        FROM LibraryFiles;
        """;


        using var reader = command.ExecuteReader();

        while(reader.Read())
        {
            files.Add(new LibraryFile
            {
                Id = reader.GetInt64(0),

                FilePath = reader.GetString(1),

                FileName = reader.IsDBNull(2)
                    ? ""
                    : reader.GetString(2),

                Extension = reader.IsDBNull(3)
                    ? ""
                    : reader.GetString(3),

                Artist = reader.IsDBNull(4)
                    ? ""
                    : reader.GetString(4),

                Album = reader.IsDBNull(5)
                    ? ""
                    : reader.GetString(5),

                Title = reader.IsDBNull(6)
                    ? ""
                    : reader.GetString(6)
            });
        }


        return files;
    }


    public List<LibraryFile> GetMissingHashes()
    {
        var files = new List<LibraryFile>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            Id,
            FilePath,
            FileName
        FROM LibraryFiles
        WHERE
            Md5Hash IS NULL
            OR Md5Hash = ''
            OR Sha256Hash IS NULL
            OR Sha256Hash = '';
        """;


        using var reader = command.ExecuteReader();

        while(reader.Read())
        {
            files.Add(new LibraryFile
            {
                Id = reader.GetInt64(0),
                FilePath = reader.GetString(1),
                FileName = reader.IsDBNull(2)
                    ? ""
                    : reader.GetString(2)
            });
        }


        return files;
    }


    // Slim projection of every row for the missing-file analysis:
    // just enough to check disk existence and match renamed files
    // by name or size, without loading full metadata for 200k+ rows.
    public List<LibraryFile> GetAllForMissingFileAnalysis()
    {
        var files = new List<LibraryFile>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            Id,
            FilePath,
            FileName,
            FileSize
        FROM LibraryFiles;
        """;


        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            files.Add(new LibraryFile
            {
                Id = reader.GetInt64(0),
                FilePath = reader.GetString(1),
                FileName = reader.IsDBNull(2)
                    ? ""
                    : reader.GetString(2),
                FileSize = reader.IsDBNull(3)
                    ? 0
                    : reader.GetInt64(3)
            });
        }


        return files;
    }


    // Deletes rows in a single transaction, together with everything
    // that references them (Issues, duplicate group memberships,
    // organization plans - all meaningless once the row is gone).
    // Used by the stale-entry cleanup to remove LibraryFiles rows
    // whose file was renamed on disk and re-imported under a new row.
    public long DeleteBatch(List<long> ids)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        long deleted = 0;

        var referencingDeletes = new[]
        {
            "DELETE FROM Issues WHERE LibraryFileId = $id;",
            "DELETE FROM DuplicateMembers WHERE LibraryFileId = $id;",
            "DELETE FROM DuplicateMatches WHERE LibraryFileId = $id;",
            "DELETE FROM OrganizationPlans WHERE LibraryFileId = $id;",
            "DELETE FROM LibraryFiles WHERE Id = $id;"
        };

        var commands = new List<SqliteCommand>();

        try
        {
            foreach (var commandText in referencingDeletes)
            {
                var command = connection.CreateCommand();

                command.CommandText = commandText;

                var idParam = command.CreateParameter();
                idParam.ParameterName = "$id";
                command.Parameters.Add(idParam);

                command.Prepare();

                commands.Add(command);
            }

            foreach (var id in ids)
            {
                foreach (var command in commands)
                {
                    command.Parameters["$id"].Value = id;

                    var affected = command.ExecuteNonQuery();

                    if (command == commands[^1])
                    {
                        deleted += affected;
                    }
                }
            }
        }
        finally
        {
            foreach (var command in commands)
            {
                command.Dispose();
            }
        }

        transaction.Commit();

        return deleted;
    }


    // Repoints rows at their new on-disk location in a single
    // transaction. Callers are responsible for making sure no target
    // path is already used by another row (FilePath is UNIQUE).
    public long UpdateFilePathsBatch(
        List<(long Id, string NewPath, string NewFileName)> updates)
    {
        if (updates.Count == 0)
        {
            return 0;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        long updated = 0;

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
            """
            UPDATE LibraryFiles
            SET
                FilePath = $path,
                FileName = $fileName
            WHERE Id = $id;
            """;

            var pathParam = command.CreateParameter();
            pathParam.ParameterName = "$path";
            command.Parameters.Add(pathParam);

            var fileNameParam = command.CreateParameter();
            fileNameParam.ParameterName = "$fileName";
            command.Parameters.Add(fileNameParam);

            var idParam = command.CreateParameter();
            idParam.ParameterName = "$id";
            command.Parameters.Add(idParam);

            command.Prepare();

            foreach (var update in updates)
            {
                pathParam.Value = update.NewPath;
                fileNameParam.Value = update.NewFileName;
                idParam.Value = update.Id;

                updated += command.ExecuteNonQuery();
            }
        }

        transaction.Commit();

        return updated;
    }


    // Live rows (file known to exist) for the tag re-read pass.
    public List<LibraryFile> GetFilesForTagRefresh()
    {
        var files = new List<LibraryFile>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            Id,
            FilePath,
            FileName
        FROM LibraryFiles
        WHERE IntegrityStatus IS NULL
           OR IntegrityStatus != 'FileNotFound';
        """;


        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            files.Add(new LibraryFile
            {
                Id = reader.GetInt64(0),
                FilePath = reader.GetString(1),
                FileName = reader.IsDBNull(2)
                    ? ""
                    : reader.GetString(2)
            });
        }


        return files;
    }


    // Applies freshly-read tag metadata in a single transaction.
    // Descriptive fields (artist, genre, BPM, ...) only fill blanks -
    // values already in the database (e.g. from AcoustID) are never
    // overwritten by a possibly-empty tag. Technical audio properties
    // are always updated since the file itself is the authority there.
    public void UpdateTagMetadataBatch(List<LibraryFile> files)
    {
        if (files.Count == 0)
        {
            return;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET
            Artist = CASE WHEN (Artist IS NULL OR Artist = '') THEN $artist ELSE Artist END,
            Album = CASE WHEN (Album IS NULL OR Album = '') THEN $album ELSE Album END,
            Title = CASE WHEN (Title IS NULL OR Title = '') THEN $title ELSE Title END,
            AlbumArtist = CASE WHEN (AlbumArtist IS NULL OR AlbumArtist = '') THEN $albumArtist ELSE AlbumArtist END,
            Genre = CASE WHEN (Genre IS NULL OR Genre = '') THEN $genre ELSE Genre END,
            Composer = CASE WHEN (Composer IS NULL OR Composer = '') THEN $composer ELSE Composer END,
            Comment = CASE WHEN (Comment IS NULL OR Comment = '') THEN $comment ELSE Comment END,
            Year = CASE WHEN (Year IS NULL OR Year = 0) THEN $year ELSE Year END,
            TrackNumber = CASE WHEN (TrackNumber IS NULL OR TrackNumber = 0) THEN $trackNumber ELSE TrackNumber END,
            DiscNumber = CASE WHEN (DiscNumber IS NULL OR DiscNumber = 0) THEN $discNumber ELSE DiscNumber END,
            Bpm = CASE WHEN (Bpm IS NULL OR Bpm = 0) THEN $bpm ELSE Bpm END,
            MusicalKey = CASE WHEN (MusicalKey IS NULL OR MusicalKey = '') THEN $musicalKey ELSE MusicalKey END,
            MusicBrainzRecordingId = CASE WHEN (MusicBrainzRecordingId IS NULL OR MusicBrainzRecordingId = '') THEN $mbRecordingId ELSE MusicBrainzRecordingId END,
            MusicBrainzReleaseId = CASE WHEN (MusicBrainzReleaseId IS NULL OR MusicBrainzReleaseId = '') THEN $mbReleaseId ELSE MusicBrainzReleaseId END,
            MusicBrainzArtistId = CASE WHEN (MusicBrainzArtistId IS NULL OR MusicBrainzArtistId = '') THEN $mbArtistId ELSE MusicBrainzArtistId END,

            DurationSeconds = $durationSeconds,
            BitRate = $bitRate,
            SampleRate = $sampleRate,
            Channels = $channels,
            Codec = $codec,
            Container = $container,
            HasArtwork = $hasArtwork,
            HasLyrics = $hasLyrics,
            FileSize = $fileSize,
            ModifiedDate = $modifiedDate,
            LastMetadataRefresh = $lastMetadataRefresh
        WHERE Id = $id;
        """;

        var parameterNames = new[]
        {
            "$artist", "$album", "$title", "$albumArtist", "$genre",
            "$composer", "$comment", "$year", "$trackNumber",
            "$discNumber", "$bpm", "$musicalKey", "$mbRecordingId",
            "$mbReleaseId", "$mbArtistId", "$durationSeconds",
            "$bitRate", "$sampleRate", "$channels", "$codec",
            "$container", "$hasArtwork", "$hasLyrics", "$fileSize",
            "$modifiedDate", "$lastMetadataRefresh", "$id"
        };

        foreach (var name in parameterNames)
        {
            AddParam(command, name);
        }

        command.Prepare();

        foreach (var file in files)
        {
            command.Parameters["$artist"].Value = file.Artist ?? "";
            command.Parameters["$album"].Value = file.Album ?? "";
            command.Parameters["$title"].Value = file.Title ?? "";
            command.Parameters["$albumArtist"].Value = file.AlbumArtist ?? "";
            command.Parameters["$genre"].Value = file.Genre ?? "";
            command.Parameters["$composer"].Value = file.Composer ?? "";
            command.Parameters["$comment"].Value = file.Comment ?? "";
            command.Parameters["$year"].Value = file.Year ?? 0;
            command.Parameters["$trackNumber"].Value = file.TrackNumber ?? 0;
            command.Parameters["$discNumber"].Value = file.DiscNumber ?? 0;
            command.Parameters["$bpm"].Value = file.Bpm ?? 0;
            command.Parameters["$musicalKey"].Value = file.MusicalKey ?? "";
            command.Parameters["$mbRecordingId"].Value = file.MusicBrainzRecordingId ?? "";
            command.Parameters["$mbReleaseId"].Value = file.MusicBrainzReleaseId ?? "";
            command.Parameters["$mbArtistId"].Value = file.MusicBrainzArtistId ?? "";
            command.Parameters["$durationSeconds"].Value = file.DurationSeconds ?? 0;
            command.Parameters["$bitRate"].Value = file.BitRate ?? 0;
            command.Parameters["$sampleRate"].Value = file.SampleRate ?? 0;
            command.Parameters["$channels"].Value = file.Channels ?? 0;
            command.Parameters["$codec"].Value = file.Codec ?? "";
            command.Parameters["$container"].Value = file.Container ?? "";
            command.Parameters["$hasArtwork"].Value = file.HasArtwork ? 1 : 0;
            command.Parameters["$hasLyrics"].Value = file.HasLyrics ? 1 : 0;
            command.Parameters["$fileSize"].Value = file.FileSize;
            command.Parameters["$modifiedDate"].Value = file.ModifiedDate?.ToString() ?? "";
            command.Parameters["$lastMetadataRefresh"].Value = file.LastMetadataRefresh?.ToString() ?? "";
            command.Parameters["$id"].Value = file.Id;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }


    // Files that haven't had an audio integrity (decode) check yet.
    // Failed files are NOT retried automatically - a file that failed
    // to decode will fail again until it is replaced, and re-testing
    // corrupt files on every run would waste most of the batch.
    public List<LibraryFile> GetFilesNeedingIntegrityCheck()
    {
        var files = new List<LibraryFile>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            Id,
            FilePath,
            FileName
        FROM LibraryFiles
        WHERE IntegrityStatus IS NULL
           OR IntegrityStatus = ''
           OR IntegrityStatus = 'Pending';
        """;


        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            files.Add(new LibraryFile
            {
                Id = reader.GetInt64(0),
                FilePath = reader.GetString(1),
                FileName = reader.IsDBNull(2)
                    ? ""
                    : reader.GetString(2)
            });
        }


        return files;
    }


    // Writes integrity check outcomes in a single transaction,
    // following the same batching pattern as UpdateHashesBatch.
    public void UpdateIntegrityResultsBatch(
        List<(long Id, string Status, string? Error)> updates)
    {
        if (updates.Count == 0)
        {
            return;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET
            IntegrityStatus = $status,
            IntegrityCheckedDate = $checkedDate,
            IntegrityError = $error
        WHERE Id = $id;
        """;

        var statusParam = command.CreateParameter();
        statusParam.ParameterName = "$status";
        command.Parameters.Add(statusParam);

        var checkedDateParam = command.CreateParameter();
        checkedDateParam.ParameterName = "$checkedDate";
        command.Parameters.Add(checkedDateParam);

        var errorParam = command.CreateParameter();
        errorParam.ParameterName = "$error";
        command.Parameters.Add(errorParam);

        var idParam = command.CreateParameter();
        idParam.ParameterName = "$id";
        command.Parameters.Add(idParam);

        command.Prepare();

        var checkedDate = DateTime.UtcNow.ToString("o");

        foreach (var update in updates)
        {
            statusParam.Value = update.Status;
            checkedDateParam.Value = checkedDate;
            errorParam.Value = update.Error ?? "";
            idParam.Value = update.Id;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }


    // Rebuilds the CorruptAudio issues from current integrity results,
    // mirroring how MetadataAnalyzer replaces its auto-detected issues
    // on each run so the Issues table always reflects current state.
    public long SyncCorruptAudioIssues()
    {
        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText =
                "DELETE FROM Issues WHERE IssueType = 'CorruptAudio';";

            deleteCommand.ExecuteNonQuery();
        }

        long inserted;

        using (var insertCommand = connection.CreateCommand())
        {
            insertCommand.CommandText =
            """
            INSERT INTO Issues (LibraryFileId, IssueType, Description, DetectedDate)
            SELECT
                Id,
                'CorruptAudio',
                COALESCE(IntegrityError, 'Failed to decode'),
                $detectedDate
            FROM LibraryFiles
            WHERE IntegrityStatus = 'Failed';
            """;

            insertCommand.Parameters.AddWithValue(
                "$detectedDate",
                DateTime.UtcNow.ToString("o"));

            inserted = insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();

        return inserted;
    }


    // Every live file that hasn't been fingerprinted yet, regardless
    // of metadata state - used by the full-library identification
    // campaign (fingerprints unlock MusicBrainz year/genre enrichment
    // even for files whose basic tags are already complete).
    public List<LibraryFile> GetFilesNeedingFingerprint()
    {
        var files = new List<LibraryFile>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            Id,
            FilePath,
            FileName
        FROM LibraryFiles
        WHERE (FingerprintStatus IS NULL
               OR FingerprintStatus = ''
               OR FingerprintStatus = 'Pending')
          AND (IntegrityStatus IS NULL
               OR IntegrityStatus != 'FileNotFound');
        """;


        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            files.Add(new LibraryFile
            {
                Id = reader.GetInt64(0),
                FilePath = reader.GetString(1),
                FileName = reader.IsDBNull(2)
                    ? ""
                    : reader.GetString(2)
            });
        }


        return files;
    }


    // Distinct MusicBrainz recording ids for rows still missing Year
    // and/or Genre - the work queue for MusicBrainz enrichment. Each
    // id is looked up once and the result applied to every row that
    // shares it, so re-runs naturally skip completed work.
    public List<string> GetRecordingIdsNeedingEnrichment()
    {
        var ids = new List<string>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT DISTINCT MusicBrainzRecordingId
        FROM LibraryFiles
        WHERE MusicBrainzRecordingId IS NOT NULL
          AND MusicBrainzRecordingId != ''
          AND (QuarantinedDate IS NULL OR QuarantinedDate = '')
          AND
          (
              Year IS NULL OR Year = 0
              OR Genre IS NULL OR Genre = ''
          );
        """;


        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            ids.Add(reader.GetString(0));
        }


        return ids;
    }


    // Fills Year/Genre (blanks only) for every row sharing a
    // MusicBrainz recording id.
    public int UpdateYearGenreByRecordingId(
        string recordingId,
        int? year,
        string? genre)
    {
        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET
            Year = CASE WHEN (Year IS NULL OR Year = 0) THEN $year ELSE Year END,
            Genre = CASE WHEN (Genre IS NULL OR Genre = '') THEN $genre ELSE Genre END
        WHERE MusicBrainzRecordingId = $recordingId;
        """;

        command.Parameters.AddWithValue("$year", year ?? 0);
        command.Parameters.AddWithValue("$genre", genre ?? "");
        command.Parameters.AddWithValue("$recordingId", recordingId);

        return command.ExecuteNonQuery();
    }


    // Live rows still missing Artist and/or Title - candidates for
    // filename parsing, the lowest-confidence fill source (70% per
    // the spec), which therefore runs after tags and AcoustID.
    public List<LibraryFile> GetFilesForFilenameParsing()
    {
        var files = new List<LibraryFile>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            Id,
            FilePath,
            FileName,
            Artist,
            Title
        FROM LibraryFiles
        WHERE (IntegrityStatus IS NULL OR IntegrityStatus != 'FileNotFound')
          AND
          (
              Artist IS NULL OR Artist = ''
              OR Title IS NULL OR Title = ''
          );
        """;


        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            files.Add(new LibraryFile
            {
                Id = reader.GetInt64(0),
                FilePath = reader.GetString(1),
                FileName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Artist = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Title = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }


        return files;
    }


    // Applies filename-parsed metadata (blanks only) in one
    // transaction, raising MetadataConfidence to the spec's 70%
    // filename-parsing tier for rows that had no confidence at all.
    public void UpdateParsedMetadataBatch(
        List<(long Id, string? Artist, string? Title, int? TrackNumber)> updates)
    {
        if (updates.Count == 0)
        {
            return;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET
            Artist = CASE WHEN (Artist IS NULL OR Artist = '') THEN $artist ELSE Artist END,
            Title = CASE WHEN (Title IS NULL OR Title = '') THEN $title ELSE Title END,
            TrackNumber = CASE WHEN (TrackNumber IS NULL OR TrackNumber = 0) THEN $trackNumber ELSE TrackNumber END,
            MetadataConfidence = CASE WHEN MetadataConfidence < 0.7 THEN 0.7 ELSE MetadataConfidence END
        WHERE Id = $id;
        """;

        AddParam(command, "$artist");
        AddParam(command, "$title");
        AddParam(command, "$trackNumber");
        AddParam(command, "$id");

        command.Prepare();

        foreach (var update in updates)
        {
            command.Parameters["$artist"].Value = update.Artist ?? "";
            command.Parameters["$title"].Value = update.Title ?? "";
            command.Parameters["$trackNumber"].Value = update.TrackNumber ?? 0;
            command.Parameters["$id"].Value = update.Id;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }


    // Every distinct artist name the library already knows, with how
    // many files carry it - the dictionary for word-order-tolerant
    // artist matching (file counts let the matcher ignore junk
    // one-off artist strings).
    public List<(string Artist, long FileCount)> GetKnownArtists()
    {
        var artists = new List<(string, long)>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT Artist, COUNT(*) FROM LibraryFiles
        WHERE Artist IS NOT NULL AND Artist != ''
        GROUP BY Artist
        UNION ALL
        SELECT AlbumArtist, COUNT(*) FROM LibraryFiles
        WHERE AlbumArtist IS NOT NULL AND AlbumArtist != ''
        GROUP BY AlbumArtist;
        """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            artists.Add((reader.GetString(0), reader.GetInt64(1)));
        }

        return artists;
    }


    // Applies dictionary-match corrections: sets Artist (only where
    // still blank) and REPLACES Title with the remainder. Overwriting
    // Title is deliberate here - for these rows the existing title was
    // itself a filename guess that still contains the artist's name,
    // so the correction strictly improves it.
    public void UpdateArtistTitleBatch(
        List<(long Id, string Artist, string Title)> updates)
    {
        if (updates.Count == 0)
        {
            return;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET
            Artist = CASE WHEN (Artist IS NULL OR Artist = '') THEN $artist ELSE Artist END,
            Title = $title,
            MetadataConfidence = CASE WHEN MetadataConfidence < 0.7 THEN 0.7 ELSE MetadataConfidence END
        WHERE Id = $id;
        """;

        AddParam(command, "$artist");
        AddParam(command, "$title");
        AddParam(command, "$id");

        command.Prepare();

        foreach (var update in updates)
        {
            command.Parameters["$artist"].Value = update.Artist;
            command.Parameters["$title"].Value = update.Title;
            command.Parameters["$id"].Value = update.Id;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }


    // Live rows with an artist or album artist set, for tag cleanup.
    public List<LibraryFile> GetArtistCleanupRows()
    {
        var files = new List<LibraryFile>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            Id,
            FileName,
            COALESCE(Artist, ''),
            COALESCE(AlbumArtist, '')
        FROM LibraryFiles
        WHERE (IntegrityStatus IS NULL OR IntegrityStatus != 'FileNotFound')
          AND (Artist != '' OR AlbumArtist != '');
        """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            files.Add(new LibraryFile
            {
                Id = reader.GetInt64(0),
                FileName = reader.GetString(1),
                Artist = reader.GetString(2),
                AlbumArtist = reader.GetString(3)
            });
        }

        return files;
    }


    // Applies artist tag cleanup: Artist and AlbumArtist are
    // overwritten with their cleaned (possibly empty) values; Title
    // only when the cleanup re-derived one.
    public void UpdateArtistCleanupBatch(
        List<(long Id, string Artist, string AlbumArtist, string? Title)> updates)
    {
        if (updates.Count == 0)
        {
            return;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET
            Artist = $artist,
            AlbumArtist = $albumArtist,
            Title = CASE WHEN $title != '' THEN $title ELSE Title END
        WHERE Id = $id;
        """;

        AddParam(command, "$artist");
        AddParam(command, "$albumArtist");
        AddParam(command, "$title");
        AddParam(command, "$id");

        command.Prepare();

        foreach (var update in updates)
        {
            command.Parameters["$artist"].Value = update.Artist;
            command.Parameters["$albumArtist"].Value = update.AlbumArtist;
            command.Parameters["$title"].Value = update.Title ?? "";
            command.Parameters["$id"].Value = update.Id;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }


    // Renames artist spellings globally (Artist and AlbumArtist), one
    // transaction for the whole batch.
    public long UpdateArtistRenameBatch(
        List<(string OldName, string NewName)> renames)
    {
        if (renames.Count == 0)
        {
            return 0;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        long updated = 0;

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
            """
            UPDATE LibraryFiles
            SET
                Artist = CASE WHEN Artist = $old THEN $new ELSE Artist END,
                AlbumArtist = CASE WHEN AlbumArtist = $old THEN $new ELSE AlbumArtist END
            WHERE Artist = $old OR AlbumArtist = $old;
            """;

            AddParam(command, "$old");
            AddParam(command, "$new");

            command.Prepare();

            foreach (var (oldName, newName) in renames)
            {
                command.Parameters["$old"].Value = oldName;
                command.Parameters["$new"].Value = newName;

                updated += command.ExecuteNonQuery();
            }
        }

        transaction.Commit();

        return updated;
    }


    // Writes corrected artist/title pairs for rows whose fields were
    // stored reversed.
    public void UpdateArtistTitleSwapBatch(
        List<(long Id, string Artist, string Title)> swaps)
    {
        if (swaps.Count == 0)
        {
            return;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET Artist = $artist, Title = $title
        WHERE Id = $id;
        """;

        AddParam(command, "$artist");
        AddParam(command, "$title");
        AddParam(command, "$id");

        command.Prepare();

        foreach (var (id, artist, title) in swaps)
        {
            command.Parameters["$artist"].Value = artist;
            command.Parameters["$title"].Value = title;
            command.Parameters["$id"].Value = id;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }


    // Rows whose file is confirmed gone from disk (set by the audio
    // integrity check) - the purge candidates for the fix engine.
    public List<long> GetDeadRowIds()
    {
        var ids = new List<long>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT Id FROM LibraryFiles WHERE IntegrityStatus = 'FileNotFound';";

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
    }


    // Issue counts grouped by type and status for the review screen.
    public List<(string IssueType, string Status, long Count)> GetIssueSummary()
    {
        var summary = new List<(string, string, long)>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            IssueType,
            COALESCE(NULLIF(Status, ''), 'Open'),
            COUNT(*)
        FROM Issues
        GROUP BY 1, 2
        ORDER BY 3 DESC;
        """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            summary.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2)));
        }

        return summary;
    }


    // Corrupt files with their decode errors, for the re-download
    // export.
    public List<(string FilePath, string Error)> GetCorruptFiles()
    {
        var files = new List<(string, string)>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT FilePath, COALESCE(IntegrityError, '')
        FROM LibraryFiles
        WHERE IntegrityStatus = 'Failed'
        ORDER BY FilePath;
        """;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            files.Add((reader.GetString(0), reader.GetString(1)));
        }

        return files;
    }


    // Records quarantine moves in one transaction: FilePath follows
    // the file to its quarantine location, OriginalPath remembers
    // where it lived, QuarantinedDate marks when. Restore = move the
    // file back and clear both fields.
    public void UpdateQuarantineBatch(
        List<(long Id, string QuarantinePath, string OriginalPath)> updates)
    {
        if (updates.Count == 0)
        {
            return;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET
            FilePath = $quarantinePath,
            OriginalPath = $originalPath,
            QuarantinedDate = $quarantinedDate
        WHERE Id = $id;
        """;

        AddParam(command, "$quarantinePath");
        AddParam(command, "$originalPath");
        AddParam(command, "$quarantinedDate");
        AddParam(command, "$id");

        command.Prepare();

        var quarantinedDate = DateTime.UtcNow.ToString("o");

        foreach (var update in updates)
        {
            command.Parameters["$quarantinePath"].Value = update.QuarantinePath;
            command.Parameters["$originalPath"].Value = update.OriginalPath;
            command.Parameters["$quarantinedDate"].Value = quarantinedDate;
            command.Parameters["$id"].Value = update.Id;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }


    // Files missing Artist, Album, and/or Title that haven't been
    // through AcoustID identification yet (MusicBrainzStatus is still
    // 'Pending'). This makes the identification job resumable: files
    // already attempted (matched or not) are skipped on the next run.
    public List<LibraryFile> GetFilesNeedingIdentification()
    {
        var files = new List<LibraryFile>();

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        SELECT
            Id,
            FilePath,
            FileName
        FROM LibraryFiles
        WHERE
        (
            Artist IS NULL OR Artist = ''
            OR Album IS NULL OR Album = ''
            OR Title IS NULL OR Title = ''
        )
        AND (MusicBrainzStatus IS NULL OR MusicBrainzStatus = 'Pending');
        """;


        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            files.Add(new LibraryFile
            {
                Id = reader.GetInt64(0),
                FilePath = reader.GetString(1),
                FileName = reader.IsDBNull(2)
                    ? ""
                    : reader.GetString(2)
            });
        }


        return files;
    }


    // Saves the outcome of an AcoustID identification attempt. Only
    // fills in Artist/Album/Title where they were previously blank -
    // existing tags are never overwritten, consistent with the "never
    // blindly change things" design of the app. Always records the
    // fingerprint/match info and status, matched or not, so the file
    // isn't retried every run.
    public void UpdateIdentificationResult(
        long id,
        string fingerprint,
        string? acoustIdId,
        double confidence,
        string? musicBrainzRecordingId,
        string? artist,
        string? album,
        string? title,
        string musicBrainzStatus)
    {
        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET
            AcoustIdFingerprint = $fingerprint,
            AcoustIdId = $acoustIdId,
            AcoustIdConfidence = $confidence,
            MusicBrainzRecordingId = $mbRecordingId,
            FingerprintStatus = 'Complete',
            FingerprintGeneratedDate = $generatedDate,
            MusicBrainzStatus = $mbStatus,
            Artist = CASE WHEN (Artist IS NULL OR Artist = '') THEN $artist ELSE Artist END,
            Album = CASE WHEN (Album IS NULL OR Album = '') THEN $album ELSE Album END,
            Title = CASE WHEN (Title IS NULL OR Title = '') THEN $title ELSE Title END
        WHERE Id = $id;
        """;

        command.Parameters.AddWithValue("$fingerprint", fingerprint);
        command.Parameters.AddWithValue("$acoustIdId", (object?)acoustIdId ?? "");
        command.Parameters.AddWithValue("$confidence", confidence);
        command.Parameters.AddWithValue("$mbRecordingId", (object?)musicBrainzRecordingId ?? "");
        command.Parameters.AddWithValue("$generatedDate", DateTime.UtcNow.ToString("o"));
        command.Parameters.AddWithValue("$mbStatus", musicBrainzStatus);
        command.Parameters.AddWithValue("$artist", (object?)artist ?? "");
        command.Parameters.AddWithValue("$album", (object?)album ?? "");
        command.Parameters.AddWithValue("$title", (object?)title ?? "");
        command.Parameters.AddWithValue("$id", id);

        command.ExecuteNonQuery();
    }


    public void UpdateHashes(
        long id,
        string md5,
        string sha256)
    {
        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET
            Md5Hash = $md5,
            Sha256Hash = $sha256
        WHERE Id = $id;
        """;


        command.Parameters.AddWithValue("$md5", md5);
        command.Parameters.AddWithValue("$sha256", sha256);
        command.Parameters.AddWithValue("$id", id);

        command.ExecuteNonQuery();
    }


    // Writes many hash updates in a single transaction instead of one
    // transaction (and one disk sync) per file. For large batches this
    // is dramatically faster than calling UpdateHashes() in a loop,
    // since SQLite's per-commit fsync cost dominates at that scale.
    public void UpdateHashesBatch(
        List<(long Id, string Md5, string Sha256)> updates)
    {
        if (updates.Count == 0)
        {
            return;
        }

        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        UPDATE LibraryFiles
        SET
            Md5Hash = $md5,
            Sha256Hash = $sha256
        WHERE Id = $id;
        """;

        var md5Param = command.CreateParameter();
        md5Param.ParameterName = "$md5";
        command.Parameters.Add(md5Param);

        var sha256Param = command.CreateParameter();
        sha256Param.ParameterName = "$sha256";
        command.Parameters.Add(sha256Param);

        var idParam = command.CreateParameter();
        idParam.ParameterName = "$id";
        command.Parameters.Add(idParam);

        command.Prepare();

        foreach (var update in updates)
        {
            md5Param.Value = update.Md5;
            sha256Param.Value = update.Sha256;
            idParam.Value = update.Id;

            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }


    public long GetCount()
    {
        using var connection = _databaseService.GetConnection();

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT COUNT(*) FROM LibraryFiles;";

        return (long)(command.ExecuteScalar() ?? 0);
    }


    public LibraryStatistics GetStatistics()
    {
        var stats = new LibraryStatistics();

        using var connection = _databaseService.GetConnection();

        connection.Open();


        // Core counts: total files, distinct artists, distinct albums.
        // Blank/NULL values are excluded from the distinct counts so they
        // don't get counted as a phantom "unknown" artist/album.

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
            """
            SELECT
                COUNT(*),
                COUNT(DISTINCT NULLIF(Artist, '')),
                COUNT(DISTINCT NULLIF(Album, ''))
            FROM LibraryFiles;
            """;

            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                stats.TotalFiles = reader.GetInt64(0);
                stats.ArtistCount = reader.GetInt64(1);
                stats.AlbumCount = reader.GetInt64(2);
            }
        }


        // File type breakdown, grouped by extension.

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
            """
            SELECT
                UPPER(REPLACE(COALESCE(Extension, ''), '.', '')) AS Ext,
                COUNT(*)
            FROM LibraryFiles
            GROUP BY Ext
            ORDER BY COUNT(*) DESC;
            """;

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var extension = reader.IsDBNull(0)
                    ? ""
                    : reader.GetString(0);

                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = "UNKNOWN";
                }

                stats.FileTypeCounts[extension] = reader.GetInt64(1);
            }
        }


        // Metadata health: how many files are missing key tags.

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
            """
            SELECT
                SUM(CASE WHEN Artist IS NULL OR Artist = '' THEN 1 ELSE 0 END),
                SUM(CASE WHEN Album IS NULL OR Album = '' THEN 1 ELSE 0 END),
                SUM(CASE WHEN Title IS NULL OR Title = '' THEN 1 ELSE 0 END)
            FROM LibraryFiles;
            """;

            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                stats.MissingArtist = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                stats.MissingAlbum = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                stats.MissingTitle = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
            }
        }


        // Hash status: how many files have MD5 / SHA256 already computed.

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
            """
            SELECT
                SUM(CASE WHEN Md5Hash IS NOT NULL AND Md5Hash != '' THEN 1 ELSE 0 END),
                SUM(CASE WHEN Sha256Hash IS NOT NULL AND Sha256Hash != '' THEN 1 ELSE 0 END)
            FROM LibraryFiles;
            """;

            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                stats.Md5Complete = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                stats.Sha256Complete = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
            }
        }


        // Database health: duplicate groups and open issues.
        // These tables may not exist yet on older databases, so each
        // lookup is wrapped defensively and just falls back to 0.

        stats.DuplicateGroups = TryGetTableCount(connection, "DuplicateGroups");
        stats.Issues = TryGetTableCount(connection, "Issues");


        return stats;
    }


    private static long TryGetTableCount(
        SqliteConnection connection,
        string tableName)
    {
        try
        {
            using var command = connection.CreateCommand();

            command.CommandText =
                $"SELECT COUNT(*) FROM {tableName};";

            return (long)(command.ExecuteScalar() ?? 0);
        }
        catch (SqliteException)
        {
            return 0;
        }
    }
}