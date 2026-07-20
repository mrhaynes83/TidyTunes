using TidyTunes.Data.Repositories;

namespace TidyTunes.Scanner.Services;

public class MetadataRefreshService
{
    private readonly LibraryFileRepository _repository;
    private readonly MetadataExtractor _extractor;


    public MetadataRefreshService(
        LibraryFileRepository repository,
        MetadataExtractor extractor)
    {
        _repository = repository;
        _extractor = extractor;
    }


    public int RefreshAll()
    {
        var files = _repository.GetAll();

        Console.WriteLine();
        Console.WriteLine(
            $"Found {files.Count:N0} files to refresh.");
        Console.WriteLine();


        var processed = 0;


        foreach (var file in files)
        {
            processed++;


            Console.WriteLine(
                $"[{processed:N0}/{files.Count:N0}] {file.FileName}");


            try
            {
                if (!File.Exists(file.FilePath))
                {
                    Console.WriteLine("Missing file.");
                    continue;
                }


                var metadata =
                    _extractor.Extract(file.FilePath);


                _repository.AddOrUpdate(metadata);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Error: {ex.Message}");
            }
        }


        Console.WriteLine();
        Console.WriteLine(
            $"Metadata refresh complete: {processed:N0} processed.");

        return processed;
    }
}