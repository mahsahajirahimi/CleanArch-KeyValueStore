using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Infrastructure.Storage.InMemory;

public sealed class InMemoryKeyValueEntryRepository
    : IKeyValueEntryRepository
{
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;

    private readonly Dictionary<(string Keyspace, string Key),
        KeyValueEntry> _entries = new();

    public InMemoryKeyValueEntryRepository(
        TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public KeyValueEntry? Get(string keyspace, string key)
    {
        lock (_sync)
        {
            var address = (keyspace, key);

            if (!_entries.TryGetValue(address, out var entry))
                return null;

            if (entry.IsExpired(_timeProvider.GetUtcNow()))
            {
                _entries.Remove(address);
                return null;
            }

            return Copy(entry);
        }
    }

    public void Save(string keyspace, KeyValueEntry entry)
    {
        lock (_sync)
        {
            _entries[(keyspace, entry.Key)] = Copy(entry);
        }
    }

    public bool Delete(string keyspace, string key)
    {
        lock (_sync)
        {
            var address = (keyspace, key);

            if (!_entries.TryGetValue(address, out var entry))
                return false;

            if (entry.IsExpired(_timeProvider.GetUtcNow()))
            {
                _entries.Remove(address);
                return false;
            }

            return _entries.Remove(address);
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

            if (_entries.TryGetValue(address, out var entry)
                && !entry.IsExpired(now))
            {
                var updateTime = now < entry.UpdatedAt
                    ? entry.UpdatedAt
                    : now;

                var updatedEntry = Copy(entry);

                updatedEntry.UpdateValue(value, updateTime);
                updatedEntry.UpdateExpiration(expiresAt);
                updatedEntry.UpdateMetadata(metadata);

                _entries[address] = updatedEntry;
            }
            else
            {
                _entries[address] = new KeyValueEntry(
                    key,
                    value,
                    now,
                    expiresAt,
                    metadata);
            }
        }
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