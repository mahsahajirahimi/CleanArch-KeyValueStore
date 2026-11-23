
using System.Text;
using System.Text.Json;

namespace KeyValueStore.Infrastructure.Storage.Persistent;

internal sealed class JsonFileStore
{
    private readonly string _filePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public JsonFileStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(
                "File path cannot be empty.",
                nameof(filePath));

        _filePath = Path.GetFullPath(filePath);
    }

    public List<StoredEntry> Load()
    {
        if (!File.Exists(_filePath))
            return new List<StoredEntry>();

        string json = File.ReadAllText(_filePath);

        return JsonSerializer.Deserialize<List<StoredEntry>>(
            json,
            JsonOptions)
            ?? throw new JsonException(
                "Storage file contains null instead of an entry list.");
    }

    public void Save(IReadOnlyCollection<StoredEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        string json = JsonSerializer.Serialize(entries, JsonOptions);

        string directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException(
                "Invalid storage file path.");

        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                using (var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(false),
                    bufferSize: 4096,
                    leaveOpen: true))
                {
                    writer.Write(json);
                    writer.Flush();
                }

                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
