
using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Infrastructure.Storage.Persistent;

public sealed class PersistentKeyValueEntryRepository
    : IKeyValueEntryRepository
{
    private readonly object _sync = new();
    private readonly JsonFileStore _fileStore;

    private Dictionary<(string Keyspace, string Key), KeyValueEntry>
        _entries = new();

    public PersistentKeyValueEntryRepository(string filePath)
    {
        _fileStore = new JsonFileStore(filePath);

        foreach (var stored in _fileStore.Load())
        {
            var entry = ToDomain(stored);

            _entries.Add(
                (stored.Keyspace, stored.Key),
                entry);
        }
    }

    public KeyValueEntry? Get(string keyspace, string key)
    {
        lock (_sync)
        {
            if (!_entries.TryGetValue((keyspace, key), out var entry))
                return null;

            if (entry.IsExpired(DateTimeOffset.UtcNow))
                return null;

            return Copy(entry);
        }
    }

    public void Save(string keyspace, KeyValueEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_sync)
        {
            var next = new Dictionary<
                (string Keyspace, string Key), KeyValueEntry>(_entries);

            next[(keyspace, entry.Key)] = Copy(entry);

            Persist(next);
            _entries = next;
        }
    }

    public bool Delete(string keyspace, string key)
    {
        lock (_sync)
        {
            var address = (keyspace, key);

            if (!_entries.ContainsKey(address))
                return false;

            var next = new Dictionary<
                (string Keyspace, string Key), KeyValueEntry>(_entries);

            next.Remove(address);

            Persist(next);
            _entries = next;

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
        lock (_sync)
        {
            var address = (keyspace, key);
            KeyValueEntry updatedEntry;

            if (_entries.TryGetValue(address, out var existing)
                && !existing.IsExpired(now))
            {
                updatedEntry = Copy(existing);

                var updateTime = now < updatedEntry.UpdatedAt
                    ? updatedEntry.UpdatedAt
                    : now;

                updatedEntry.UpdateValue(value, updateTime);
                updatedEntry.UpdateExpiration(expiresAt);
                updatedEntry.UpdateMetadata(metadata);
            }
            else
            {
                updatedEntry = new KeyValueEntry(
                    key, value, now, expiresAt, metadata);
            }

            var next = new Dictionary<
                (string Keyspace, string Key), KeyValueEntry>(_entries);

            next[address] = updatedEntry;

            Persist(next);
            _entries = next;
        }
    }

    private void Persist(
        Dictionary<(string Keyspace, string Key), KeyValueEntry> entries)
    {
        var snapshot = new List<StoredEntry>();

        foreach (var pair in entries)
        {
            var entry = pair.Value;

            snapshot.Add(new StoredEntry(
                pair.Key.Keyspace,
                entry.Key,
                entry.Value,
                entry.CreatedAt,
                entry.UpdatedAt,
                entry.ExpiresAt,
                new Dictionary<string, string>(entry.Metadata)));
        }

        _fileStore.Save(snapshot);
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

    private static KeyValueEntry Copy(KeyValueEntry source)
    {
        var copy = new KeyValueEntry(
            source.Key,
            source.Value,
            source.CreatedAt,
            source.ExpiresAt,
            source.Metadata);

        if (source.UpdatedAt != source.CreatedAt)
            copy.UpdateValue(source.Value, source.UpdatedAt);

        return copy;
    }
}
