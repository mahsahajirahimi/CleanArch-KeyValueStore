using Microsoft.Extensions.Configuration;

namespace KeyValueStore.Api.Configuration;

public sealed class StorageOptions
{
    public string Mode { get; }
    public string? FilePath { get; }
    public int CacheCapacity { get; }

    private StorageOptions(
        string mode,
        string? filePath,
        int cacheCapacity)
    {
        Mode = mode;
        FilePath = filePath;
        CacheCapacity = cacheCapacity;
    }

    public static StorageOptions FromConfiguration(
        IConfiguration configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            throw new ArgumentException(
                "Content root path cannot be empty.",
                nameof(contentRootPath));
        }

        string mode =
            configuration["Storage:Mode"]?.Trim()
            ?? "InMemory";

        if (!mode.Equals(
                "InMemory",
                StringComparison.OrdinalIgnoreCase)
            && !mode.Equals(
                "Persistent",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported storage mode: '{mode}'.");
        }

        int cacheCapacity = 100;

        string? capacityValue =
            configuration["Storage:CacheCapacity"];

        if (capacityValue is not null)
        {
            if (!int.TryParse(
                    capacityValue,
                    out cacheCapacity)
                || cacheCapacity <= 0)
            {
                throw new InvalidOperationException(
                    "Storage:CacheCapacity must be a positive integer.");
            }
        }

        string? filePath = null;

        if (mode.Equals(
            "Persistent",
            StringComparison.OrdinalIgnoreCase))
        {
            string? configuredPath =
                configuration["Storage:FilePath"];

            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                throw new InvalidOperationException(
                    "Persistent storage requires a file path.");
            }

            filePath = Path.GetFullPath(
                configuredPath,
                Path.GetFullPath(contentRootPath));
        }

        return new StorageOptions(
            mode,
            filePath,
            cacheCapacity);
    }
}