
namespace KeyValueStore.Api.Configuration;

public sealed class StorageOptions
{
    public string Mode { get; }
    public string? FilePath { get; }

    private StorageOptions(string mode, string? filePath)
    {
        Mode = mode;
        FilePath = filePath;
    }

    public static StorageOptions FromConfiguration(
        IConfiguration configuration,
        string contentRootPath)
    {
        string mode = configuration["Storage:Mode"] ?? "InMemory";

        if (mode.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
            return new StorageOptions("InMemory", null);

        if (!mode.Equals("Persistent", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Unsupported storage mode: {mode}");

        string? filePath = configuration["Storage:FilePath"];

        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException(
                "Storage:FilePath is required for Persistent mode.");

        string fullPath = Path.GetFullPath(filePath, contentRootPath);

        return new StorageOptions("Persistent", fullPath);
    }
}