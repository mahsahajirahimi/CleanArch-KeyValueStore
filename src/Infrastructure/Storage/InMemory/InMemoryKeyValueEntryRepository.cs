using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Infrastructure.Storage.InMemory;

public sealed class InMemoryKeyValueEntryRepository
    : IKeyValueEntryRepository
{
    private readonly object _sync = new();

    private readonly Dictionary<(string Keyspace, string Key),
        KeyValueEntry> _entries = new();

    public KeyValueEntry? Get(string keyspace, string key)
    {
        lock (_sync)
        {
            var address = (keyspace, key);

            if (!_entries.TryGetValue(address, out var entry))
                return null;

            if (entry.IsExpired(DateTimeOffset.UtcNow))
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
            return _entries.Remove((keyspace, key));
        }
    }

    public void Upsert(
        string keyspace,
        string key,
        string value,
        DateTimeOffset now)
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

                entry.UpdateValue(value, updateTime);
            }
            else
            {
                _entries[address] =
                    new KeyValueEntry(key, value, now);
            }
        }
    }

    private static KeyValueEntry Copy(KeyValueEntry source)
    {
        var copy = new KeyValueEntry(
            source.Key,
            source.Value,
            source.CreatedAt,
            source.ExpiresAt);

        if (source.UpdatedAt != source.CreatedAt)
            copy.UpdateValue(source.Value, source.UpdatedAt);

        return copy;
    }
}