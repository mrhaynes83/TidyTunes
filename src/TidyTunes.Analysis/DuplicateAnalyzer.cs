using Microsoft.Data.Sqlite;
using TidyTunes.Core.Models;
using TidyTunes.Data;

namespace TidyTunes.Analysis;

public class DuplicateAnalyzer
{
    private readonly DatabaseService _database;

    public DuplicateAnalyzer(DatabaseService database)
    {
        _database = database;
    }


    // Finds exact duplicates: files whose full byte content hashes
    // identically (same SHA256). Persists the results into the
    // DuplicateGroups / DuplicateMatches tables and returns them
    // for reporting. Re-running this replaces the previous exact-match
    // results, since it's meant to reflect the current state of the
    // library each time it's run, not accumulate stale groups.
    public List<DuplicateGroupResult> FindExactDuplicates()
    {
        var groups = new List<DuplicateGroupResult>();

        using var connection = _database.GetConnection();

        connection.Open();

        using var transaction = connection.BeginTransaction();


        // Pull every file that belongs to a hash shared by more than
        // one row. Only files with a real SHA256 already computed are
        // considered — run Database Maintenance first if this comes
        // back empty.

        using (var readCommand = connection.CreateCommand())
        {
            readCommand.CommandText =
            """
            SELECT
                Sha256Hash,
                Id,
                FilePath,
                FileName,
                FileSize,
                IntegrityStatus,
                BitRate
            FROM LibraryFiles
            WHERE Sha256Hash IN
            (
                SELECT Sha256Hash
                FROM LibraryFiles
                WHERE Sha256Hash IS NOT NULL
                  AND Sha256Hash <> ''
                GROUP BY Sha256Hash
                HAVING COUNT(*) > 1
            )
            ORDER BY Sha256Hash;
            """;

            using var reader = readCommand.ExecuteReader();

            DuplicateGroupResult? currentGroup = null;

            while (reader.Read())
            {
                var hash = reader.GetString(0);

                if (currentGroup is null || currentGroup.Hash != hash)
                {
                    currentGroup = new DuplicateGroupResult
                    {
                        Hash = hash,
                        FileSize = reader.IsDBNull(4) ? 0 : reader.GetInt64(4)
                    };

                    groups.Add(currentGroup);
                }

                currentGroup.Files.Add(new LibraryFile
                {
                    Id = reader.GetInt64(1),
                    FilePath = reader.GetString(2),
                    FileName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    FileSize = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                    IntegrityStatus = reader.IsDBNull(5) ? null : reader.GetString(5),
                    BitRate = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                });
            }
        }


        // Replace any previous exact-match results with this fresh run.

        using (var clearMatches = connection.CreateCommand())
        {
            clearMatches.CommandText =
            """
            DELETE FROM DuplicateMatches
            WHERE DuplicateGroupId IN
            (
                SELECT Id FROM DuplicateGroups
                WHERE DuplicateType = 'ExactMatch:SHA256'
            );
            """;

            clearMatches.ExecuteNonQuery();
        }

        using (var clearGroups = connection.CreateCommand())
        {
            clearGroups.CommandText =
            """
            DELETE FROM DuplicateGroups
            WHERE DuplicateType = 'ExactMatch:SHA256';
            """;

            clearGroups.ExecuteNonQuery();
        }


        foreach (var group in groups)
        {
            long groupId;

            using (var insertGroup = connection.CreateCommand())
            {
                insertGroup.CommandText =
                """
                INSERT INTO DuplicateGroups
                (
                    CreatedDate,
                    DuplicateType,
                    FileCount
                )
                VALUES
                (
                    $createdDate,
                    'ExactMatch:SHA256',
                    $fileCount
                );

                SELECT last_insert_rowid();
                """;

                insertGroup.Parameters.AddWithValue("$createdDate", DateTime.UtcNow.ToString("o"));
                insertGroup.Parameters.AddWithValue("$fileCount", group.Files.Count);

                groupId = (long)(insertGroup.ExecuteScalar() ?? 0L);
            }

            foreach (var file in group.Files)
            {
                using var insertMatch = connection.CreateCommand();

                insertMatch.CommandText =
                """
                INSERT INTO DuplicateMatches
                (
                    DuplicateGroupId,
                    LibraryFileId,
                    MatchMethod,
                    Confidence
                )
                VALUES
                (
                    $groupId,
                    $fileId,
                    'SHA256',
                    1.0
                );
                """;

                insertMatch.Parameters.AddWithValue("$groupId", groupId);
                insertMatch.Parameters.AddWithValue("$fileId", file.Id);

                insertMatch.ExecuteNonQuery();
            }
        }

        transaction.Commit();

        return groups;
    }
}
