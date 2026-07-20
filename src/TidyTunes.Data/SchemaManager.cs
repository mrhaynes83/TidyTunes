using Microsoft.Data.Sqlite;

namespace TidyTunes.Data;

public class SchemaManager
{
    private readonly DatabaseService _databaseService;

    public SchemaManager(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }


    public void EnsureSchema()
    {
        using var connection = _databaseService.GetConnection();
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText =
        """
        CREATE TABLE IF NOT EXISTS LibraryFiles
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,

            FilePath TEXT NOT NULL UNIQUE,
            FileName TEXT,
            Extension TEXT,

            FileSize INTEGER,
            ModifiedDate TEXT,

            Artist TEXT,
            Album TEXT,
            Title TEXT,
            Genre TEXT,
            Year INTEGER,
            TrackNumber INTEGER,
            DiscNumber INTEGER,
            Composer TEXT,
            Comment TEXT,

            FileHash TEXT,
            AudioHash TEXT,

            DurationSeconds INTEGER,
            BitRate INTEGER,
            SampleRate INTEGER,
            Channels INTEGER,

            AnalysisStatus TEXT,
            AnalysisDate TEXT,
            AnalysisError TEXT,

            QualityScore INTEGER,
            QualityFlags TEXT,

            OrganizationStatus TEXT,
            ProposedPath TEXT,

            CoverArtStatus TEXT,

            DateAdded TEXT NOT NULL
        );


        CREATE TABLE IF NOT EXISTS DuplicateGroups
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,

            CreatedDate TEXT NOT NULL,
            DuplicateType TEXT,
            Status TEXT
        );


        CREATE TABLE IF NOT EXISTS DuplicateMembers
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,

            DuplicateGroupId INTEGER NOT NULL,
            LibraryFileId INTEGER NOT NULL,

            FOREIGN KEY(DuplicateGroupId)
                REFERENCES DuplicateGroups(Id),

            FOREIGN KEY(LibraryFileId)
                REFERENCES LibraryFiles(Id)
        );


        CREATE TABLE IF NOT EXISTS ScanHistory
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,

            StartTime TEXT NOT NULL,
            EndTime TEXT,

            FilesScanned INTEGER,
            Status TEXT
        );


        CREATE TABLE IF NOT EXISTS AnalysisHistory
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,

            StartTime TEXT NOT NULL,
            EndTime TEXT,

            FilesProcessed INTEGER,
            FilesSuccessful INTEGER,
            FilesFailed INTEGER,

            Status TEXT
        );


        CREATE TABLE IF NOT EXISTS OrganizationPlans
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,

            LibraryFileId INTEGER NOT NULL,

            OldPath TEXT,
            NewPath TEXT,

            Status TEXT,

            FOREIGN KEY(LibraryFileId)
                REFERENCES LibraryFiles(Id)
        );


        CREATE TABLE IF NOT EXISTS Issues
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,

            LibraryFileId INTEGER,

            IssueType TEXT NOT NULL,
            Description TEXT,

            DetectedDate TEXT NOT NULL,

            FOREIGN KEY(LibraryFileId)
                REFERENCES LibraryFiles(Id)
        );


        CREATE TABLE IF NOT EXISTS Settings
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,

            Key TEXT UNIQUE NOT NULL,
            Value TEXT
        );


        CREATE TABLE IF NOT EXISTS BackupHistory
        (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,

            FilePath TEXT,
            BackupPath TEXT,

            BackupDate TEXT,
            Status TEXT
        );
        """;

        command.ExecuteNonQuery();
    }
}