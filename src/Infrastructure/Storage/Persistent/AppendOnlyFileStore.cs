using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyValueStore.Infrastructure.Storage.Persistent;

internal sealed class AppendOnlyFileStore
{
    private const int MaxRecordBytes = 16 * 1024 * 1024;

    private readonly string _filePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter<LogOperation>() }
    };

    public AppendOnlyFileStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(
                "File path cannot be empty.",
                nameof(filePath));

        _filePath = Path.GetFullPath(filePath);
    }

    public long Append(LogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        byte[] data = JsonSerializer.SerializeToUtf8Bytes(
            record,
            JsonOptions);

        if (data.Length > MaxRecordBytes)
            throw new ArgumentException(
                "Log record exceeds the maximum allowed size.",
                nameof(record));

        string directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException(
                "Invalid log file path.");

        Directory.CreateDirectory(directory);

        using var stream = new FileStream(
            _filePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

        long offset = stream.Seek(0, SeekOrigin.End);

        using (var writer = new BinaryWriter(
            stream,
            Encoding.UTF8,
            leaveOpen: true))
        {
            writer.Write(data.Length);
            writer.Write(data);
            writer.Flush();
        }

        stream.Flush(flushToDisk: true);

        return offset;
    }

    public LogRecord ReadAt(long offset)
    {
        using var stream = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        if (offset < 0 || offset > stream.Length - sizeof(int))
            throw new InvalidDataException(
                "Invalid log record offset.");

        stream.Seek(offset, SeekOrigin.Begin);

        using var reader = new BinaryReader(
            stream,
            Encoding.UTF8,
            leaveOpen: true);

        return ReadRecord(reader);
    }

    public void Replay(Action<long, LogRecord> onRecord)
    {
        ArgumentNullException.ThrowIfNull(onRecord);

        if (!File.Exists(_filePath))
            return;

        using var stream = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        using var reader = new BinaryReader(
            stream,
            Encoding.UTF8,
            leaveOpen: true);

        while (stream.Position < stream.Length)
        {
            long offset = stream.Position;

            LogRecord record = ReadRecord(reader);

            onRecord(offset, record);
        }
    }

    public string? RecoverIncompleteTail()
    {
        if (!File.Exists(_filePath))
            return null;

        using var stream = new FileStream(
            _filePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);

        using var reader = new BinaryReader(
            stream,
            Encoding.UTF8,
            leaveOpen: true);

        while (stream.Position < stream.Length)
        {
            long offset = stream.Position;

            // Not enough bytes for a complete length header.
            if (stream.Length - offset < sizeof(int))
                return BackupAndTruncate(stream, offset);

            int length = reader.ReadInt32();

            if (length <= 0 || length > MaxRecordBytes)
                throw new InvalidDataException(
                    $"Invalid record length at offset {offset}.");

            // Header is valid, but the payload is incomplete.
            if (length > stream.Length - stream.Position)
                return BackupAndTruncate(stream, offset);

            // Validate the complete record before continuing.
            stream.Position = offset;
            _ = ReadRecord(reader);
        }

        // Every record was complete and readable.
        return null;
    }

    private string BackupAndTruncate(
        FileStream stream,
        long validLength)
    {
        string backupPath = _filePath +
            $".recovery-{Guid.NewGuid():N}.tail";

        stream.Position = validLength;

        using (var backup = new FileStream(
            backupPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            stream.CopyTo(backup);
            backup.Flush(flushToDisk: true);
        }

        // Truncate only after the backup was written.
        stream.SetLength(validLength);
        stream.Flush(flushToDisk: true);

        return backupPath;
    }

    private static LogRecord ReadRecord(BinaryReader reader)
    {
        Stream stream = reader.BaseStream;

        if (stream.Length - stream.Position < sizeof(int))
            throw new InvalidDataException(
                "Incomplete log record header.");

        int length = reader.ReadInt32();

        if (length <= 0 ||
            length > MaxRecordBytes ||
            length > stream.Length - stream.Position)
        {
            throw new InvalidDataException(
                "Incomplete or invalid log record.");
        }

        byte[] data = reader.ReadBytes(length);

        if (data.Length != length)
            throw new InvalidDataException(
                "Log record is incomplete.");

        try
        {
            return JsonSerializer.Deserialize<LogRecord>(
                data,
                JsonOptions)
                ?? throw new InvalidDataException(
                    "Log record cannot be null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Log record contains invalid JSON.",
                ex);
        }
    }
}