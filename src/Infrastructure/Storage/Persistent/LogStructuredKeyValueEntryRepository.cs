using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Infrastructure.Storage.Persistent;

public sealed class LogStructuredKeyValueEntryRepository
    : IKeyValueEntryRepository
{
    private readonly object _sync = new();
    private readonly AppendOnlyFileStore _log;
    private readonly BPlusTree _index = new();

    public LogStructuredKeyValueEntryRepository(string filePath)
    {
        _log = new AppendOnlyFileStore(filePath);

        _log.RecoverIncompleteTail();

        _log.Replay((offset, record) =>
        {
            if (string.IsNullOrWhiteSpace(record.Keyspace) ||
                string.IsNullOrWhiteSpace(record.Key))
            {
                throw new InvalidDataException(
                    "Log record has an invalid key.");
            }

            switch (record.Operation)
            {
                case LogOperation.Set:
                {
                    StoredEntry stored = RequireStoredEntry(record);

                    if (stored.ExpiresAt <= DateTimeOffset.UtcNow)
                        _index.Delete(record.Keyspace, record.Key);
                    else
                        _index.Upsert(
                            record.Keyspace, record.Key, offset);

                    break;
                }

                case LogOperation.Delete:
                    _index.Delete(record.Keyspace, record.Key);
                    break;

                default:
                    throw new InvalidDataException(
                        "Unknown log operation.");
            }
        });
    }

    public KeyValueEntry? Get(string keyspace, string key)
    {
        ValidateAddress(keyspace, key);

        lock (_sync)
        {
            return LoadActiveEntry(
                keyspace, key, DateTimeOffset.UtcNow);
        }
    }

    public void Save(string keyspace, KeyValueEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateAddress(keyspace, entry.Key);

        lock (_sync)
        {
            AppendSet(keyspace, entry);
        }
    }

    public bool Delete(string keyspace, string key)
    {
        ValidateAddress(keyspace, key);

        lock (_sync)
        {
            if (_index.FindOffset(keyspace, key) is null)
                return false;

            _log.Append(new LogRecord(
                LogOperation.Delete,
                keyspace,
                key,
                null));

            _index.Delete(keyspace, key);

            return true;
        }
    }

    public void Upsert(
        string keyspace,
        string key,
        string value,
        DateTimeOffset now,
        DateTimeOffset? expiresAt = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ValidateAddress(keyspace, key);

        lock (_sync)
        {
            var entry = LoadActiveEntry(keyspace, key, now);

            if (entry is null)
            {
                entry = new KeyValueEntry(
                    key, value, now, expiresAt, metadata);
            }
            else
            {
                var updateTime = now < entry.UpdatedAt
                    ? entry.UpdatedAt
                    : now;

                entry.UpdateValue(value, updateTime);
                entry.UpdateExpiration(expiresAt);
                entry.UpdateMetadata(metadata);
            }

            AppendSet(keyspace, entry);
        }
    }

    private KeyValueEntry? LoadActiveEntry(
        string keyspace,
        string key,
        DateTimeOffset now)
    {
        long? offset = _index.FindOffset(keyspace, key);

        if (offset is null)
            return null;

        LogRecord record = _log.ReadAt(offset.Value);

        if (record.Operation != LogOperation.Set ||
            record.Keyspace != keyspace ||
            record.Key != key)
        {
            throw new InvalidDataException(
                "Index points to an incorrect log record.");
        }

        var entry = ToDomain(RequireStoredEntry(record));

        if (entry.IsExpired(now))
        {
            _index.Delete(keyspace, key);
            return null;
        }

        return entry;
    }

    private void AppendSet(string keyspace, KeyValueEntry entry)
    {
        var stored = new StoredEntry(
            keyspace,
            entry.Key,
            entry.Value,
            entry.CreatedAt,
            entry.UpdatedAt,
            entry.ExpiresAt,
            new Dictionary<string, string>(entry.Metadata));

        long offset = _log.Append(new LogRecord(
            LogOperation.Set,
            keyspace,
            entry.Key,
            stored));

        if (entry.IsExpired(DateTimeOffset.UtcNow))
            _index.Delete(keyspace, entry.Key);
        else
            _index.Upsert(keyspace, entry.Key, offset);
    }

    private static StoredEntry RequireStoredEntry(LogRecord record)
    {
        if (record.Entry is null ||
            record.Entry.Keyspace != record.Keyspace ||
            record.Entry.Key != record.Key)
        {
            throw new InvalidDataException(
                "Log record contains an invalid entry.");
        }

        return record.Entry;
    }

    private static KeyValueEntry ToDomain(StoredEntry stored)
    {
        var entry = new KeyValueEntry(
            stored.Key,
            stored.Value,
            stored.CreatedAt,
            stored.ExpiresAt,
            stored.Metadata);

        if (stored.UpdatedAt != stored.CreatedAt)
            entry.UpdateValue(stored.Value, stored.UpdatedAt);

        return entry;
    }

    private static void ValidateAddress(string keyspace, string key)
    {
        if (string.IsNullOrWhiteSpace(keyspace))
            throw new ArgumentException(
                "Keyspace cannot be empty.", nameof(keyspace));

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException(
                "Key cannot be empty.", nameof(key));
    }
}