namespace KeyValueStore.Infrastructure.Storage;

public sealed class DataDirectoryLock : IDisposable
{
    private const string LockFileName = "bootstrap.lock";

    private readonly FileStream _stream;

    private DataDirectoryLock(FileStream stream)
    {
        _stream = stream;
    }

    public static DataDirectoryLock Acquire(
        string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException(
                "Data directory cannot be empty.",
                nameof(dataDirectory));
        }

        string fullDirectory = Path.GetFullPath(dataDirectory);

        Directory.CreateDirectory(fullDirectory);

        string lockPath = Path.Combine(
            fullDirectory,
            LockFileName);

        try
        {
            var stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            return new DataDirectoryLock(stream);
        }
        catch (IOException ex)
        {
            throw new IOException(
                $"Cannot acquire exclusive lock for data directory " +
                $"'{fullDirectory}'. " +
                "It may already be in use by another API or " +
                "BootstrapAdmin process.",
                ex);
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}