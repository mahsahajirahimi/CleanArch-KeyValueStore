using KeyValueStore.Application.Abstractions;
using KeyValueStore.Domain.Aggregates;

namespace KeyValueStore.Infrastructure.Storage.InMemory;

public sealed class InMemoryKeyValueEntryRepository
    : IKeyValueEntryRepository
{
    private readonly Dictionary<(string Keyspace, string Key), KeyValueEntry> _entries = new();

    public KeyValueEntry? Get(string keyspace, string key)
    {
        if (!_entries.TryGetValue((keyspace, key), out var entry))
            return null;

        if (entry.IsExpired(DateTimeOffset.UtcNow))
        {
            _entries.Remove((keyspace, key));
            return null;
        }

        return entry;
    }

    public void Save(string keyspace, KeyValueEntry entry)
    {
        _entries[(keyspace, entry.Key)] = entry;
    }

    public bool Delete(string keyspace, string key)
    {
        return _entries.Remove((keyspace, key));
    }
}