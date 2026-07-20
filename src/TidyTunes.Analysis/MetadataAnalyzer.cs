using Microsoft.Data.Sqlite;
using TidyTunes.Core.Models;
using TidyTunes.Data;

namespace TidyTunes.Analysis;

public class MetadataAnalyzer
{
    private readonly DatabaseService _database;

    public MetadataAnalyzer(DatabaseService database)
    {
        _database = database;
    }


    // Finds files missing Artist, Album, and/or Title, and records each
    // gap as a row in the Issues table so it can be reviewed/fixed later
    // from the Cleanup menu. Re-running this replaces the previous
    // auto-detected metadata issues, so it always reflects current state
    // (e.g. after a metadata refresh fixes some files).
    public MetadataGapReport FindMissingMetadata()
    {
        var report = new MetadataGapReport();

        var gaps = new List<(
            long Id,
            string FilePath,
            string FileName,
            bool MissingArtist,
            bool MissingAlbum,
            bool MissingTitle)>();

        using var connection = _database.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();


        using (var readCommand = connection.CreateCommand())
        {
            readCommand.CommandText =
            """
            SELECT
                Id,
                FilePath,
                FileName,
                CASE WHEN Artist IS NULL OR Artist = '' THEN 1 ELSE 0 END,
                CASE WHEN Album IS NULL OR Album = '' THEN 1 ELSE 0 END,
                CASE WHEN Title IS NULL OR Title = '' THEN 1 ELSE 0 END
            FROM LibraryFiles
            WHERE (Artist IS NULL OR Artist = '')
               OR (Album IS NULL OR Album = '')
               OR (Title IS NULL OR Title = '');
            """;

            using var reader = readCommand.ExecuteReader();

            while (reader.Read())
            {
                gaps.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.GetInt32(3) == 1,
                    reader.GetInt32(4) == 1,
                    reader.GetInt32(5) == 1
                ));
            }
        }


        // Replace previous auto-detected metadata issues with this
        // fresh run. Other issue types (e.g. future Quality Issues)
        // are left untouched.

        using (var clearCommand = connection.CreateCommand())
        {
            clearCommand.CommandText =
            """
            DELETE FROM Issues
            WHERE IssueType IN ('MissingArtist', 'MissingAlbum', 'MissingTitle');
            """;

            clearCommand.ExecuteNonQuery();
        }


        var detectedDate = DateTime.UtcNow.ToString("o");

        using var insertCommand = connection.CreateCommand();

        insertCommand.CommandText =
        """
        INSERT INTO Issues
        (
            LibraryFileId,
            IssueType,
            Description,
            DetectedDate
        )
        VALUES
        (
            $fileId,
            $issueType,
            $description,
            $detectedDate
        );
        """;

        var fileIdParam = insertCommand.CreateParameter();
        fileIdParam.ParameterName = "$fileId";
        insertCommand.Parameters.Add(fileIdParam);

        var issueTypeParam = insertCommand.CreateParameter();
        issueTypeParam.ParameterName = "$issueType";
        insertCommand.Parameters.Add(issueTypeParam);

        var descriptionParam = insertCommand.CreateParameter();
        descriptionParam.ParameterName = "$description";
        insertCommand.Parameters.Add(descriptionParam);

        var detectedDateParam = insertCommand.CreateParameter();
        detectedDateParam.ParameterName = "$detectedDate";
        detectedDateParam.Value = detectedDate;
        insertCommand.Parameters.Add(detectedDateParam);

        insertCommand.Prepare();


        foreach (var gap in gaps)
        {
            report.TotalFilesWithGaps++;

            if (gap.MissingArtist)
            {
                report.MissingArtistCount++;

                fileIdParam.Value = gap.Id;
                issueTypeParam.Value = "MissingArtist";
                descriptionParam.Value = $"Artist tag is missing ({gap.FileName})";

                insertCommand.ExecuteNonQuery();
            }

            if (gap.MissingAlbum)
            {
                report.MissingAlbumCount++;

                fileIdParam.Value = gap.Id;
                issueTypeParam.Value = "MissingAlbum";
                descriptionParam.Value = $"Album tag is missing ({gap.FileName})";

                insertCommand.ExecuteNonQuery();
            }

            if (gap.MissingTitle)
            {
                report.MissingTitleCount++;

                fileIdParam.Value = gap.Id;
                issueTypeParam.Value = "MissingTitle";
                descriptionParam.Value = $"Title tag is missing ({gap.FileName})";

                insertCommand.ExecuteNonQuery();
            }

            if (gap.MissingArtist && gap.MissingAlbum && gap.MissingTitle)
            {
                report.MissingAllCount++;

                report.WorstOffenders.Add(new LibraryFile
                {
                    Id = gap.Id,
                    FilePath = gap.FilePath,
                    FileName = gap.FileName
                });
            }
        }

        transaction.Commit();

        return report;
    }
}
